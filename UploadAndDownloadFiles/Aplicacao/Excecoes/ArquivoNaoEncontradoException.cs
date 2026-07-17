namespace UploadAndDownloadFiles.Aplicacao.Excecoes;

public sealed class ArquivoNaoEncontradoException : Exception
{
    public ArquivoNaoEncontradoException(Guid id)
        : base($"Arquivo '{id}' não encontrado.")
    {
    }
}
