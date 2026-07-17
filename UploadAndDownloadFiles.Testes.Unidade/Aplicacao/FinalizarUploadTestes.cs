using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class FinalizarUploadTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task ComEtagsValidos_CompletaGravaTamanhoRealEMarcaCompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var partes = new[] { new ParteEnviada(1, "etag-1") };

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200 * Mb);

        var casoDeUso = new FinalizarUpload(repositorio.Object, armazenamento.Object);

        await casoDeUso.ExecutarAsync(arquivo.Id, partes);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(200 * Mb, arquivo.TamanhoReal);
        armazenamento.Verify(a => a.CompletarMultipartAsync(arquivo.Chave, "upload-id-123", partes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChamadoDuasVezes_EhIdempotenteENaoChamaS3NaSegundaVez()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var partes = new[] { new ParteEnviada(1, "etag-1") };

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200 * Mb);

        var casoDeUso = new FinalizarUpload(repositorio.Object, armazenamento.Object);

        await casoDeUso.ExecutarAsync(arquivo.Id, partes);
        await casoDeUso.ExecutarAsync(arquivo.Id, partes);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        armazenamento.Verify(a => a.CompletarMultipartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ParteEnviada>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
