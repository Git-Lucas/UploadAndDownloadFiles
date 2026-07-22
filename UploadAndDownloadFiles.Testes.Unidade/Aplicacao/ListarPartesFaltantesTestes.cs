using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class ListarPartesFaltantesTestes
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task RetornaApenasOsNumerosDePartesAusentes()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 500 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var totalPartes = arquivo.QuantidadePartesEsperada!.Value;

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var partesJaEnviadas = Enumerable.Range(1, totalPartes)
            .Where(n => n != 2 && n != totalPartes)
            .Select(n => new ParteEnviada(n, $"etag-{n}"))
            .ToList();

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(arquivo.Chave, "upload-id-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partesJaEnviadas);

        var casoDeUso = new ListarPartesFaltantes(repositorio.Object, armazenamento.Object);

        var faltantes = await casoDeUso.ExecutarAsync(arquivo.Id);

        Assert.Equal([2, totalPartes], faltantes);
    }

    [Fact]
    public async Task RetornaListaVaziaQuandoArquivoJaEstaCompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 500 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        arquivo.Finalizar(500 * Mb);

        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio.Setup(r => r.ObterPorIdAsync(arquivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();

        var casoDeUso = new ListarPartesFaltantes(repositorio.Object, armazenamento.Object);

        var faltantes = await casoDeUso.ExecutarAsync(arquivo.Id);

        Assert.Empty(faltantes);
        armazenamento.Verify(
            a => a.ListarPartesEnviadasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
