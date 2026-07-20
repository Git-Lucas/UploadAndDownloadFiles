using UploadAndDownloadFiles.Infraestrutura.Aws;

namespace UploadAndDownloadFiles.Testes.Unidade.Infraestrutura;

public class CabecalhoContentDispositionTestes
{
    [Fact]
    public void Montar_ComAcentosEEspacos_CodificaEmUtf8NoFilenameEstrela()
    {
        var cabecalho = CabecalhoContentDisposition.Montar("Currículo Lucas de Oliveira.pdf");

        Assert.Equal(
            "attachment; filename=\"Currculo Lucas de Oliveira.pdf\"; filename*=UTF-8''Curr%C3%ADculo%20Lucas%20de%20Oliveira.pdf",
            cabecalho);
    }

    [Fact]
    public void Montar_ComNomeJaAscii_MantemONomeNasDuasFormas()
    {
        var cabecalho = CabecalhoContentDisposition.Montar("jre-8u461-windows-x64.exe");

        Assert.Equal(
            "attachment; filename=\"jre-8u461-windows-x64.exe\"; filename*=UTF-8''jre-8u461-windows-x64.exe",
            cabecalho);
    }

    [Theory]
    [InlineData("rela\"torio.pdf")]
    [InlineData("rela\\torio.pdf")]
    [InlineData("relatorio\r\n.pdf")]
    public void Montar_ComCaracteresQueQuebrariamOCabecalho_OsDescartaDoFallback(string nomeOriginal)
    {
        var cabecalho = CabecalhoContentDisposition.Montar(nomeOriginal);

        var fallback = cabecalho.Split("filename=\"")[1].Split('"')[0];
        Assert.DoesNotContain('"', fallback);
        Assert.DoesNotContain('\\', fallback);
        Assert.DoesNotContain('\r', fallback);
        Assert.DoesNotContain('\n', fallback);
    }

    [Fact]
    public void Montar_ComNomeSemNenhumCaractereAscii_UsaFallbackGenerico()
    {
        var cabecalho = CabecalhoContentDisposition.Montar("上传文件");

        Assert.StartsWith("attachment; filename=\"arquivo\";", cabecalho);
        Assert.EndsWith("filename*=UTF-8''%E4%B8%8A%E4%BC%A0%E6%96%87%E4%BB%B6", cabecalho);
    }
}
