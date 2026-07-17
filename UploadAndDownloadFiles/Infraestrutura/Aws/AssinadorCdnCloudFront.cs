using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using UploadAndDownloadFiles.Aplicacao.Portas;

namespace UploadAndDownloadFiles.Infraestrutura.Aws;

/// <summary>
/// Gera CloudFront Signed URLs de política canônica (canned policy): assina com a chave privada
/// do par de chaves de assinatura via RSA-SHA1, conforme o protocolo documentado pela AWS.
/// </summary>
public sealed class AssinadorCdnCloudFront : IAssinadorCdn
{
    private static readonly TimeSpan ExpiracaoUrlAssinada = TimeSpan.FromMinutes(15);

    private readonly OpcoesCloudFront _opcoes;
    private readonly Lazy<RSA> _chavePrivada;

    public AssinadorCdnCloudFront(IOptions<OpcoesCloudFront> opcoes)
    {
        _opcoes = opcoes.Value;
        _chavePrivada = new Lazy<RSA>(CarregarChavePrivada);
    }

    public string GerarUrlAssinada(string chave)
    {
        var urlRecurso = $"https://{_opcoes.DominioDistribuicao}/{chave}";
        var expiraEm = DateTimeOffset.UtcNow.Add(ExpiracaoUrlAssinada).ToUnixTimeSeconds();
        var politica = ConstruirPoliticaCanonica(urlRecurso, expiraEm);
        var assinatura = AssinarPolitica(politica);

        return $"{urlRecurso}?Expires={expiraEm}&Signature={assinatura}&Key-Pair-Id={_opcoes.IdParDeChaves}";
    }

    private RSA CarregarChavePrivada()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(_opcoes.CaminhoChavePrivada));
        return rsa;
    }

    private static string ConstruirPoliticaCanonica(string urlRecurso, long expiraEm)
    {
        var politica = new JsonObject
        {
            ["Statement"] = new JsonArray
            {
                new JsonObject
                {
                    ["Resource"] = urlRecurso,
                    ["Condition"] = new JsonObject
                    {
                        ["DateLessThan"] = new JsonObject
                        {
                            ["AWS:EpochTime"] = expiraEm,
                        },
                    },
                },
            },
        };

        return politica.ToJsonString();
    }

    private string AssinarPolitica(string politica)
    {
        var assinaturaBytes = _chavePrivada.Value.SignData(Encoding.UTF8.GetBytes(politica), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        return CodificarBase64UrlSeguro(assinaturaBytes);
    }

    private static string CodificarBase64UrlSeguro(byte[] dados) =>
        Convert.ToBase64String(dados)
            .Replace('+', '-')
            .Replace('=', '_')
            .Replace('/', '~');
}
