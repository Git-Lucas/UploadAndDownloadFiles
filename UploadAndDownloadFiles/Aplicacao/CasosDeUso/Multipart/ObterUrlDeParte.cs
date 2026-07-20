using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;

/// <summary>Obtém a URL pré-assinada de uma parte, sob demanda. Também serve para reassinatura.</summary>
public sealed class ObterUrlDeParte(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task<string> ExecutarAsync(Guid id, int numeroParte, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Modo != ModoUpload.Multipart || arquivo.Status != StatusArquivo.Enviando)
            throw new OperacaoInvalidaException($"Arquivo '{id}' não está em upload multipart.");

        if (numeroParte < 1 || numeroParte > arquivo.QuantidadePartesEsperada!.Value)
            throw new OperacaoInvalidaException($"Número de parte '{numeroParte}' fora do intervalo esperado (1-{arquivo.QuantidadePartesEsperada}).");

        var tamanhoDaParte = CalcularTamanhoDaParte(arquivo, numeroParte);

        return await _armazenamento.CriarUrlDeParteAsync(
            arquivo.Chave,
            arquivo.IdUploadS3!,
            numeroParte,
            tamanhoDaParte,
            cancellationToken);
    }

    /// <summary>
    /// Todas as partes têm o tamanho nominal `TamanhoParte`, exceto a última, cujo tamanho real
    /// é o restante do arquivo (pode ser menor que o nominal).
    /// </summary>
    private static long CalcularTamanhoDaParte(Dominio.Arquivo arquivo, int numeroParte)
    {
        var tamanhoParteNominal = arquivo.TamanhoParte!.Value;
        var totalPartes = arquivo.QuantidadePartesEsperada!.Value;

        if (numeroParte < totalPartes)
            return tamanhoParteNominal;

        return arquivo.TamanhoDeclarado - tamanhoParteNominal * (totalPartes - 1);
    }
}
