using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.Modelos;

public sealed record RegistroArquivoResultado(
    Guid Id,
    ModoUpload Modo,
    string? UrlUpload,
    string? CabecalhoContentDisposition,
    long? TamanhoParte,
    int? QuantidadePartesEsperada);
