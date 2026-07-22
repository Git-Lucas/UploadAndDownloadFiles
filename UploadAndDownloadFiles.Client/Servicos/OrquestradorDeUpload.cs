using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using UploadAndDownloadFiles.Shared;
using UploadAndDownloadFiles.Shared.Dtos;

namespace UploadAndDownloadFiles.Client.Servicos;

/// <summary>
/// Orquestra o fluxo de upload (registrar → enviar → finalizar), incluindo retomada por partes
/// faltantes, reassinatura de URLs expiradas e retry com backoff exponencial. Os ETags das
/// partes já enviadas são mantidos em memória ao longo da sessão de envio (uma chamada a
/// <see cref="EnviarAsync"/>): reexecutar o envio (ex.: botão "Tentar novamente" após falha
/// parcial) consulta novamente as partes faltantes e reaproveita os ETags já conhecidos.
/// </summary>
public sealed class OrquestradorDeUpload(HttpClient http, InteropDeUpload interop)
{
    private const int ConcorrenciaMaxima = 4;
    private const int TentativasMaximasPorParte = 5;
    private static readonly TimeSpan s_backoffBase = TimeSpan.FromSeconds(1);

    private readonly HttpClient _http = http;
    private readonly InteropDeUpload _interop = interop;
    private readonly ConcurrentDictionary<int, string> _etagsConhecidos = new();
    private long _tamanhoParte;

    public async Task<RegistrarArquivoResponse> RegistrarAsync(string idInputArquivo, CancellationToken cancellationToken = default)
    {
        _etagsConhecidos.Clear();

        var nome = await _interop.ObterNomeArquivoAsync(idInputArquivo)
            ?? throw new InvalidOperationException("Nenhum arquivo selecionado.");
        var tamanho = await _interop.ObterTamanhoArquivoAsync(idInputArquivo);

        var resposta = await _http.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest(nome, tamanho), cancellationToken);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<RegistrarArquivoResponse>(cancellationToken))!;
    }

    public async Task EnviarAsync(
        Guid id,
        string idInputArquivo,
        ModoUpload modo,
        string? urlUpload,
        string? cabecalhoContentDisposition,
        long? tamanhoParte,
        Action<string>? aoProgredir = null,
        Action<double>? aoAtualizarProgresso = null,
        CancellationToken cancellationToken = default)
    {
        var tamanhoTotal = await _interop.ObterTamanhoArquivoAsync(idInputArquivo);

        var relator = new RelatorDeProgresso(tamanhoTotal, pct => aoAtualizarProgresso?.Invoke(pct));
        using var referenciaRelator = DotNetObjectReference.Create(relator);

        if (modo == ModoUpload.PutUnico)
        {
            aoProgredir?.Invoke("Enviando arquivo...");
            await _interop.EnviarArquivoCompletoAsync(idInputArquivo, urlUpload!, cabecalhoContentDisposition!, referenciaRelator);

            relator.Reportar(0, tamanhoTotal);
            aoProgredir?.Invoke("Confirmando upload...");
            var respostaConfirmar = await _http.PostAsync($"/api/arquivos/put-unico/{id}/confirmar", content: null, cancellationToken);
            respostaConfirmar.EnsureSuccessStatusCode();

            aoProgredir?.Invoke("Upload concluído.");
            return;
        }

        await EnviarMultipartAsync(id, idInputArquivo, tamanhoParte!.Value, tamanhoTotal, relator, referenciaRelator, aoProgredir, cancellationToken);
    }

    private async Task EnviarMultipartAsync(
        Guid id,
        string idInputArquivo,
        long tamanhoParte,
        long tamanhoTotal,
        RelatorDeProgresso relator,
        DotNetObjectReference<RelatorDeProgresso> referenciaRelator,
        Action<string>? aoProgredir,
        CancellationToken cancellationToken)
    {
        _tamanhoParte = tamanhoParte;
        var faltantesResposta = await _http.GetFromJsonAsync<PartesFaltantesResponse>($"/api/arquivos/multipart/{id}/partes/faltantes", cancellationToken);
        var faltantes = faltantesResposta!.NumerosFaltantes;

        // Contabiliza no progresso as partes que o servidor já recebeu (retomada), para a barra
        // começar no ponto certo em vez de voltar a zero.
        var totalPartes = (int)((tamanhoTotal + tamanhoParte - 1) / tamanhoParte);
        var faltantesSet = faltantes.ToHashSet();
        for (var numeroParte = 1; numeroParte <= totalPartes; numeroParte++)
        {
            if (!faltantesSet.Contains(numeroParte))
                relator.Semear(numeroParte, TamanhoDaParte(numeroParte, tamanhoParte, tamanhoTotal));
        }
        relator.Notificar();

        aoProgredir?.Invoke($"{faltantes.Count} parte(s) a enviar.");

        using var semaforo = new SemaphoreSlim(ConcorrenciaMaxima);

        var tarefas = faltantes.Select(async numeroParte =>
        {
            await semaforo.WaitAsync(cancellationToken);
            try
            {
                var etag = await EnviarParteComRetentativaAsync(id, idInputArquivo, numeroParte, referenciaRelator, cancellationToken);
                _etagsConhecidos[numeroParte] = etag;
                relator.Semear(numeroParte, TamanhoDaParte(numeroParte, tamanhoParte, tamanhoTotal));
                aoProgredir?.Invoke($"Parte {numeroParte} enviada.");
            }
            finally
            {
                semaforo.Release();
            }
        });

        await Task.WhenAll(tarefas);

        aoProgredir?.Invoke("Finalizando upload...");

        var requisicaoFinalizar = new FinalizarUploadRequest(
            [.. _etagsConhecidos.OrderBy(p => p.Key).Select(p => new ParteEtag(p.Key, p.Value))]);

        var respostaFinalizar = await _http.PostAsJsonAsync($"/api/arquivos/multipart/{id}/finalizar", requisicaoFinalizar, cancellationToken);
        respostaFinalizar.EnsureSuccessStatusCode();

        aoProgredir?.Invoke("Upload concluído.");
    }

    private async Task<string> EnviarParteComRetentativaAsync(Guid id, string idInputArquivo, int numeroParte, DotNetObjectReference<RelatorDeProgresso> referenciaRelator, CancellationToken cancellationToken)
    {
        Exception? ultimaFalha = null;

        for (var tentativa = 1; tentativa <= TentativasMaximasPorParte; tentativa++)
        {
            try
            {
                // Sempre busca a URL sob demanda: cobre tanto o primeiro envio quanto a
                // reassinatura de uma URL expirada numa tentativa anterior.
                var urlParte = await _http.GetFromJsonAsync<UrlParteResponse>($"/api/arquivos/multipart/{id}/partes/{numeroParte}/url", cancellationToken);
                return await _interop.EnviarParteAsync(idInputArquivo, numeroParte, _tamanhoParte, urlParte!.Url, referenciaRelator);
            }
            catch (Exception ex) when (tentativa < TentativasMaximasPorParte)
            {
                ultimaFalha = ex;
                var espera = s_backoffBase * Math.Pow(2, tentativa - 1);
                await Task.Delay(espera, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Falha ao enviar a parte {numeroParte} após {TentativasMaximasPorParte} tentativas.", ultimaFalha);
    }

    private static long TamanhoDaParte(int numeroParte, long tamanhoParte, long tamanhoTotal)
    {
        var inicio = (numeroParte - 1) * tamanhoParte;
        return Math.Min(tamanhoParte, tamanhoTotal - inicio);
    }
}
