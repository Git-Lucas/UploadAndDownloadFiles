using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso;

/// <summary>
/// Reconcilia registros não finalizados (`Pendente`/`Enviando`) há mais de 24h contra o S3.
/// Para `Enviando`, verifica primeiro `HeadObject` e só então `ListParts` (cobre o caso de
/// complete bem-sucedido no S3 mas falha ao atualizar o banco).
/// </summary>
public sealed class ReconciliarArquivos(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private static readonly TimeSpan JanelaDeTolerancia = TimeSpan.FromHours(24);

    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = DateTime.UtcNow - JanelaDeTolerancia;
        var registros = await _repositorio.ObterNaoFinalizadosAntesDeAsync(limite, cancellationToken);

        foreach (var arquivo in registros)
        {
            if (arquivo.Status == StatusArquivo.Pendente)
            {
                await ReconciliarPendenteAsync(arquivo, cancellationToken);
            }
            else if (arquivo.Status == StatusArquivo.Enviando)
            {
                await ReconciliarEnviandoAsync(arquivo, cancellationToken);
            }
        }

        await _repositorio.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task ReconciliarPendenteAsync(Arquivo arquivo, CancellationToken cancellationToken)
    {
        var tamanhoReal = await _armazenamento.ObterTamanhoObjetoAsync(arquivo.Chave, cancellationToken);

        if (tamanhoReal.HasValue)
            arquivo.ReconciliarComoCompleto(tamanhoReal.Value);
        else
            arquivo.MarcarInvalido();
    }

    private async Task ReconciliarEnviandoAsync(Arquivo arquivo, CancellationToken cancellationToken)
    {
        var tamanhoReal = await _armazenamento.ObterTamanhoObjetoAsync(arquivo.Chave, cancellationToken);

        if (tamanhoReal.HasValue)
        {
            arquivo.ReconciliarComoCompleto(tamanhoReal.Value);
            return;
        }

        var partesEnviadas = await _armazenamento.ListarPartesEnviadasAsync(arquivo.Chave, arquivo.IdUploadS3!, cancellationToken);

        if (partesEnviadas.Count == arquivo.QuantidadePartesEsperada)
        {
            await _armazenamento.CompletarMultipartAsync(arquivo.Chave, arquivo.IdUploadS3!, partesEnviadas, cancellationToken);
            var tamanhoFinal = await _armazenamento.ObterTamanhoObjetoAsync(arquivo.Chave, cancellationToken);
            arquivo.ReconciliarComoCompleto(tamanhoFinal!.Value);
        }
        else
        {
            arquivo.MarcarIncompleto();
            await _armazenamento.AbortarMultipartAsync(arquivo.Chave, arquivo.IdUploadS3!, cancellationToken);
        }
    }
}
