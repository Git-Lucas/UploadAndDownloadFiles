using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Unidade.Aplicacao;

public class ReconciliarArquivosTestes
{
    private const long Mb = 1024 * 1024;

    private static Mock<IRepositorioArquivos> CriarRepositorioCom(Arquivo arquivo)
    {
        var repositorio = new Mock<IRepositorioArquivos>();
        repositorio
            .Setup(r => r.ObterNaoFinalizadosAntesDeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([arquivo]);
        return repositorio;
    }

    [Fact]
    public async Task PendenteComObjetoExistente_TornaCompletoEGravaTamanhoReal()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);
        var repositorio = CriarRepositorioCom(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento.Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>())).ReturnsAsync(10 * Mb);

        await new ReconciliarArquivos(repositorio.Object, armazenamento.Object).ExecutarAsync();

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(10 * Mb, arquivo.TamanhoReal);
    }

    [Fact]
    public async Task PendenteSemObjeto_TornaInvalido()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);
        var repositorio = CriarRepositorioCom(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento.Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

        await new ReconciliarArquivos(repositorio.Object, armazenamento.Object).ExecutarAsync();

        Assert.Equal(StatusArquivo.Inválido, arquivo.Status);
    }

    [Fact]
    public async Task EnviandoComObjetoExistente_TornaCompletoSemConsultarListParts()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var repositorio = CriarRepositorioCom(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento.Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>())).ReturnsAsync(200 * Mb);

        await new ReconciliarArquivos(repositorio.Object, armazenamento.Object).ExecutarAsync();

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        armazenamento.Verify(a => a.ListarPartesEnviadasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnviandoSemObjetoComTodasAsPartes_CompletaMultipartETornaCompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var totalPartes = arquivo.QuantidadePartesEsperada!.Value;
        var repositorio = CriarRepositorioCom(arquivo);

        var todasAsPartes = Enumerable.Range(1, totalPartes).Select(n => new ParteEnviada(n, $"etag-{n}")).ToList();

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento.SetupSequence(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null)
            .ReturnsAsync(200 * Mb);
        armazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(arquivo.Chave, "upload-id-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(todasAsPartes);

        await new ReconciliarArquivos(repositorio.Object, armazenamento.Object).ExecutarAsync();

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(200 * Mb, arquivo.TamanhoReal);
        armazenamento.Verify(a => a.CompletarMultipartAsync(arquivo.Chave, "upload-id-123", todasAsPartes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviandoSemObjetoComPartesFaltando_TornaIncompletoEAbortaMultipart()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-123");
        var repositorio = CriarRepositorioCom(arquivo);

        var armazenamento = new Mock<IArmazenamentoObjetos>();
        armazenamento.Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);
        armazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(arquivo.Chave, "upload-id-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "etag-1")]);

        await new ReconciliarArquivos(repositorio.Object, armazenamento.Object).ExecutarAsync();

        Assert.Equal(StatusArquivo.Incompleto, arquivo.Status);
        armazenamento.Verify(a => a.AbortarMultipartAsync(arquivo.Chave, "upload-id-123", It.IsAny<CancellationToken>()), Times.Once);
        armazenamento.Verify(a => a.CompletarMultipartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ParteEnviada>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
