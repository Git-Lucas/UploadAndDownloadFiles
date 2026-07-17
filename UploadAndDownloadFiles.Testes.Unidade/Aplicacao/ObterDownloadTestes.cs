using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class ObterDownloadTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task ComArquivoCompleto_RetornaUrlAssinada()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);
        arquivo.ReconciliarComoCompleto(10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var assinadorCdn = new Mock<IAssinadorCdn>();
        assinadorCdn.Setup(a => a.GerarUrlAssinada(arquivo.Chave)).Returns("https://cdn/url-assinada");

        var casoDeUso = new ObterDownload(repositorio.Object, assinadorCdn.Object);

        var url = await casoDeUso.ExecutarAsync(arquivo.Id);

        Assert.Equal("https://cdn/url-assinada", url);
    }

    [Fact]
    public async Task ComArquivoAindaNaoCompleto_LancaOperacaoInvalida()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var casoDeUso = new ObterDownload(repositorio.Object, Mock.Of<IAssinadorCdn>());

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => casoDeUso.ExecutarAsync(arquivo.Id));
    }

    [Fact]
    public async Task ComArquivoInexistente_LancaArquivoNaoEncontrado()
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Arquivo?)null);

        var casoDeUso = new ObterDownload(repositorio.Object, Mock.Of<IAssinadorCdn>());

        await Assert.ThrowsAsync<ArquivoNaoEncontradoException>(() => casoDeUso.ExecutarAsync(Guid.NewGuid()));
    }
}
