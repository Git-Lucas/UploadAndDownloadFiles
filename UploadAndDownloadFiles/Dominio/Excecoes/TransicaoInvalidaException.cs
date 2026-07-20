namespace UploadAndDownloadFiles.Dominio.Excecoes;

public sealed class TransicaoInvalidaException(string mensagem) : Exception(mensagem);
