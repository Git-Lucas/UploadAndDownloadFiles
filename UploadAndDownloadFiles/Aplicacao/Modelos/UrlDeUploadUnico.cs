namespace UploadAndDownloadFiles.Aplicacao.Modelos;

/// <summary>
/// URL pré-assinada de PUT único acompanhada do `Content-Disposition` que entrou na assinatura e
/// que o cliente precisa reenviar como cabeçalho da requisição.
/// </summary>
public sealed record UrlDeUploadUnico(string Url, string CabecalhoContentDisposition);
