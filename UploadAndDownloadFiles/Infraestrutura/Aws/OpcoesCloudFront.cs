namespace UploadAndDownloadFiles.Infraestrutura.Aws;

public sealed class OpcoesCloudFront
{
    public const string SecaoConfiguracao = "CloudFront";

    public string DominioDistribuicao { get; set; } = string.Empty;
    public string IdParDeChaves { get; set; } = string.Empty;
    public string CaminhoChavePrivada { get; set; } = string.Empty;
}
