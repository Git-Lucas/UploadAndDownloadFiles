using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class ConfirmarUploadUnicoTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task ComObjetoPresenteNoS3_MarcaCompletoEGravaTamanhoReal()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10 * Mb);

        var casoDeUso = new ConfirmarUploadUnico(repositorio.Object, armazenamento.Object);

        await casoDeUso.ExecutarAsync(arquivo.Id);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(10 * Mb, arquivo.TamanhoReal);
        repositorio.Verify(r => r.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChamadoDuasVezes_EhIdempotenteENaoChamaS3NaSegundaVez()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10 * Mb);

        var casoDeUso = new ConfirmarUploadUnico(repositorio.Object, armazenamento.Object);

        await casoDeUso.ExecutarAsync(arquivo.Id);
        await casoDeUso.ExecutarAsync(arquivo.Id);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        armazenamento.Verify(a => a.ObterTamanhoObjetoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ComObjetoAusenteNoS3_LancaOperacaoInvalida()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        var casoDeUso = new ConfirmarUploadUnico(repositorio.Object, armazenamento.Object);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => casoDeUso.ExecutarAsync(arquivo.Id));
        Assert.Equal(StatusArquivo.Pendente, arquivo.Status);
    }

    [Fact]
    public async Task ComArquivoEmModoMultipart_LancaOperacaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        var casoDeUso = new ConfirmarUploadUnico(repositorio.Object, armazenamento.Object);

        await Assert.ThrowsAsync<OperacaoInvalidaException>(() => casoDeUso.ExecutarAsync(arquivo.Id));
    }

    [Fact]
    public async Task ComArquivoInexistente_LancaArquivoNaoEncontrado()
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Arquivo?)null);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        var casoDeUso = new ConfirmarUploadUnico(repositorio.Object, armazenamento.Object);

        await Assert.ThrowsAsync<ArquivoNaoEncontradoException>(() => casoDeUso.ExecutarAsync(Guid.NewGuid()));
    }
}
