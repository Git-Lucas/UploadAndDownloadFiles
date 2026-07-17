namespace UploadAndDownloadFiles.Shared.Dtos;

public sealed record FinalizarUploadRequest(IReadOnlyList<ParteEtag> Partes);

public sealed record ParteEtag(int Numero, string ETag);
