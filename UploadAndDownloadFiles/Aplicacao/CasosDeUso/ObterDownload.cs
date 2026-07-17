using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso;

public sealed class ObterDownload
{
    private readonly IRepositorioArquivos _repositorio;
    private readonly IAssinadorCdn _assinadorCdn;

    public ObterDownload(IRepositorioArquivos repositorio, IAssinadorCdn assinadorCdn)
    {
        _repositorio = repositorio;
        _assinadorCdn = assinadorCdn;
    }

    public async Task<string> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Status != StatusArquivo.Completo)
            throw new OperacaoInvalidaException($"Arquivo '{id}' ainda não está completo.");

        return _assinadorCdn.GerarUrlAssinada(arquivo.Chave);
    }
}
