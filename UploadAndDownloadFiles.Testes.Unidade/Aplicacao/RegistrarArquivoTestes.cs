using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class RegistrarArquivoTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task ComTamanhoMenorQue100MB_UsaPutUnicoERetornaUrlUnica()
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), 50 * Mb, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3/url-unica");

        var casoDeUso = new RegistrarArquivo(repositorio.Object, armazenamento.Object);

        var resultado = await casoDeUso.ExecutarAsync("foto.png", 50 * Mb);

        Assert.Equal(ModoUpload.PutUnico, resultado.Modo);
        Assert.Equal("https://s3/url-unica", resultado.UrlUpload);
        Assert.Null(resultado.TamanhoParte);
        Assert.Null(resultado.QuantidadePartesEsperada);

        armazenamento.Verify(a => a.IniciarMultipartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repositorio.Verify(r => r.AdicionarAsync(It.Is<Arquivo>(x => x.Status == StatusArquivo.Pendente), It.IsAny<CancellationToken>()), Times.Once);
        repositorio.Verify(r => r.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ComTamanhoMaiorOuIgualA100MB_IniciaMultipartEPersisteEmEnviando()
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.IniciarMultipartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("upload-id-123");

        var casoDeUso = new RegistrarArquivo(repositorio.Object, armazenamento.Object);

        var resultado = await casoDeUso.ExecutarAsync("video.mp4", 200 * Mb);

        Assert.Equal(ModoUpload.Multipart, resultado.Modo);
        Assert.Null(resultado.UrlUpload);
        Assert.NotNull(resultado.TamanhoParte);
        Assert.NotNull(resultado.QuantidadePartesEsperada);

        armazenamento.Verify(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repositorio.Verify(
            r => r.AdicionarAsync(It.Is<Arquivo>(x => x.Status == StatusArquivo.Enviando && x.IdUploadS3 == "upload-id-123"), It.IsAny<CancellationToken>()),
            Times.Once);
        repositorio.Verify(r => r.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
