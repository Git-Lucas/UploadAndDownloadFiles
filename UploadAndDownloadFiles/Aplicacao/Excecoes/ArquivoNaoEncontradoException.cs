namespace UploadAndDownloadFiles.Aplicacao.Excecoes;

public sealed class ArquivoNaoEncontradoException(Guid id) : Exception($"Arquivo '{id}' não encontrado.");
