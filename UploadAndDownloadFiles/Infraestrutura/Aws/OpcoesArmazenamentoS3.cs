namespace UploadAndDownloadFiles.Infraestrutura.Aws;

public sealed class OpcoesArmazenamentoS3
{
    public const string SecaoConfiguracao = "ArmazenamentoS3";

    public string NomeBucket { get; set; } = string.Empty;
}
