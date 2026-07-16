namespace UploadAndDownloadFiles.Shared.Dtos;

public sealed record RegistrarArquivoResponse(
    Guid Id,
    ModoUpload Modo,
    string? UrlUpload,
    long? TamanhoParte,
    int? QuantidadePartesEsperada);
