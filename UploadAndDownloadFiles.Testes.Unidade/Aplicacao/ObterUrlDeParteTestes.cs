using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class ObterUrlDeParteTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task ComArquivoEmEnviando_RetornaUrlDaParte()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.CriarUrlDeParteAsync(arquivo.Chave, "upload-id-123", 1, arquivo.TamanhoParte!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3/url-parte-1");

        var casoDeUso = new ObterUrlDeParte(repositorio.Object, armazenamento.Object);

        var url = await casoDeUso.ExecutarAsync(arquivo.Id, 1);

        Assert.Equal("https://s3/url-parte-1", url);
    }

    [Fact]
    public async Task ComArquivoInexistente_LancaArquivoNaoEncontrado()
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Arquivo?)null);

        var casoDeUso = new ObterUrlDeParte(repositorio.Object, Mock.Of<IArmazenamentoObjetos>());

        await Assert.ThrowsAsync<ArquivoNaoEncontradoException>(() => casoDeUso.ExecutarAsync(Guid.NewGuid(), 1));
    }

    [Fact]
    public async Task ComArquivoEmPutUnico_LancaOperacaoInvalida()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var casoDeUso = new ObterUrlDeParte(repositorio.Object, Mock.Of<IArmazenamentoObjetos>());

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => casoDeUso.ExecutarAsync(arquivo.Id, 1));
    }

    [Fact]
    public async Task ComArquivoMultipartAindaPendente_LancaOperacaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var casoDeUso = new ObterUrlDeParte(repositorio.Object, Mock.Of<IArmazenamentoObjetos>());

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => casoDeUso.ExecutarAsync(arquivo.Id, 1));
    }
}
