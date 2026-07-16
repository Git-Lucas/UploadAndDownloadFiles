namespace UploadAndDownloadFiles.Dominio.Excecoes;

public sealed class TransicaoInvalidaException : Exception
{
    public TransicaoInvalidaException(string mensagem) : base(mensagem)
    {
    }
}
