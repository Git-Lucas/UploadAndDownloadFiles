namespace UploadAndDownloadFiles.Aplicacao.Excecoes;

public sealed class OperacaoInvalidaException : Exception
{
    public OperacaoInvalidaException(string mensagem) : base(mensagem)
    {
    }
}
