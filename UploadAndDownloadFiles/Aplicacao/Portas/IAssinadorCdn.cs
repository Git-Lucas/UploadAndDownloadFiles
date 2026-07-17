namespace UploadAndDownloadFiles.Aplicacao.Portas;

/// <summary>Porta para assinatura de URLs de download via CloudFront.</summary>
public interface IAssinadorCdn
{
    string GerarUrlAssinada(string chave);
}
