namespace UploadAndDownloadFiles.Aplicacao.Excecoes;

public sealed class OperacaoInvalidaException(string mensagem) : Exception(mensagem);
