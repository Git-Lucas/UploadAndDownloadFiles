namespace UploadAndDownloadFiles.Shared.Dtos;

public sealed record RegistrarArquivoResponse(
    Guid Id,
    ModoUpload Modo,
    string? UrlUpload,
    string? CabecalhoContentDisposition,
    long? TamanhoParte,
    int? QuantidadePartesEsperada);
