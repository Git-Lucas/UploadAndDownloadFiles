using System.Text;

namespace UploadAndDownloadFiles.Infraestrutura.Aws;

/// <summary>
/// Monta o valor de <c>Content-Disposition</c> gravado como metadata do objeto no S3, para que o
/// download via CloudFront salve o arquivo com o nome de exibição original — a chave do objeto é
/// sanitizada para ASCII (ver <see cref="Dominio.Arquivo"/>) e sozinha perderia acentos e espaços.
/// </summary>
/// <remarks>
/// Segue a RFC 6266: um <c>filename</c> ASCII puro como fallback para agentes antigos, seguido de
/// <c>filename*</c> em UTF-8 (RFC 5987), que tem precedência nos navegadores atuais.
/// </remarks>
public static class CabecalhoContentDisposition
{
    private const string NomeFallback = "arquivo";

    public static string Montar(string nomeOriginal)
    {
        var fallbackAscii = MontarFallbackAscii(nomeOriginal);
        var codificadoUtf8 = Uri.EscapeDataString(nomeOriginal);

        return $"attachment; filename=\"{fallbackAscii}\"; filename*=UTF-8''{codificadoUtf8}";
    }

    /// <summary>
    /// Reduz o nome a ASCII imprimível, descartando aspas e barras invertidas — que encerrariam a
    /// quoted-string do cabeçalho — e caracteres de controle.
    /// </summary>
    private static string MontarFallbackAscii(string nomeOriginal)
    {
        var caracteresSeguros = nomeOriginal
            .Where(caractere => caractere is >= ' ' and <= '~' && caractere is not ('"' or '\\'))
            .ToArray();

        var fallback = new string(caracteresSeguros).Trim();

        return fallback.Length > 0 ? fallback : NomeFallback;
    }
}
