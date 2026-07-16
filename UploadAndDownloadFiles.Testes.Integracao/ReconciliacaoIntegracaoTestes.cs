using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Infraestrutura.Persistencia;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Integracao;

/// <summary>
/// Exercita `ReconciliarArquivos` de ponta a ponta contra o `DbContext` (EF InMemory) resolvido
/// via DI, cobrindo a matriz de reconciliação para registros não finalizados há mais de 24h.
/// </summary>
public class ReconciliacaoIntegracaoTestes : IDisposable
{
    private const long Mb = 1024 * 1024;

    private readonly ArquivosWebApplicationFactory _factory = new();

    [Fact]
    public async Task RegistroPendenteAntigo_ComObjetoExistente_TornaCompleto()
    {
        var arquivo = await SemearArquivoAntigoAsync(Arquivo.Registrar("foto.png", 10 * Mb));

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10 * Mb);

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Completo, recarregado.Status);
        Assert.Equal(10 * Mb, recarregado.TamanhoReal);
    }

    [Fact]
    public async Task RegistroPendenteAntigo_SemObjeto_TornaInvalido()
    {
        var arquivo = await SemearArquivoAntigoAsync(Arquivo.Registrar("foto.png", 10 * Mb));

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Inválido, recarregado.Status);
    }

    [Fact]
    public async Task RegistroEnviandoAntigo_ComObjetoExistente_TornaCompletoSemListarPartes()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-antigo");
        await SemearArquivoAntigoAsync(arquivo);

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200 * Mb);

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Completo, recarregado.Status);
        _factory.MockArmazenamento.Verify(a => a.ListarPartesEnviadasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistroEnviandoAntigo_SemObjetoComTodasAsPartes_CompletaMultipartETornaCompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-antigo");
        var totalPartes = arquivo.QuantidadePartesEsperada!.Value;
        await SemearArquivoAntigoAsync(arquivo);

        _factory.MockArmazenamento
            .SetupSequence(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null)
            .ReturnsAsync(200 * Mb);
        _factory.MockArmazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(arquivo.Chave, "upload-id-antigo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, totalPartes).Select(n => new ParteEnviada(n, $"etag-{n}")).ToList());

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Completo, recarregado.Status);
        _factory.MockArmazenamento.Verify(
            a => a.CompletarMultipartAsync(arquivo.Chave, "upload-id-antigo", It.IsAny<IReadOnlyList<ParteEnviada>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegistroEnviandoAntigo_ComPartesFaltando_TornaIncompletoEAbortaMultipart()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * Mb);
        arquivo.IniciarMultipart("upload-id-antigo");
        await SemearArquivoAntigoAsync(arquivo);

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(arquivo.Chave, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
        _factory.MockArmazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(arquivo.Chave, "upload-id-antigo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParteEnviada> { new(1, "etag-1") });

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Incompleto, recarregado.Status);
        _factory.MockArmazenamento.Verify(a => a.AbortarMultipartAsync(arquivo.Chave, "upload-id-antigo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistroRecente_NaoEhTocadoPelaReconciliacao()
    {
        using var escopo = _factory.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ArquivosDbContext>();
        var arquivo = Arquivo.Registrar("foto.png", 10 * Mb);
        contexto.Arquivos.Add(arquivo);
        await contexto.SaveChangesAsync();

        await ExecutarReconciliacaoAsync();

        var recarregado = await RecarregarAsync(arquivo.Id);
        Assert.Equal(StatusArquivo.Pendente, recarregado.Status);
        _factory.MockArmazenamento.Verify(a => a.ObterTamanhoObjetoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<Arquivo> SemearArquivoAntigoAsync(Arquivo arquivo)
    {
        using var escopo = _factory.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ArquivosDbContext>();

        contexto.Arquivos.Add(arquivo);
        await contexto.SaveChangesAsync();

        contexto.Entry(arquivo).Property(a => a.AtualizadoEm).CurrentValue = DateTime.UtcNow.AddHours(-25);
        await contexto.SaveChangesAsync();

        return arquivo;
    }

    private async Task ExecutarReconciliacaoAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var reconciliarArquivos = escopo.ServiceProvider.GetRequiredService<ReconciliarArquivos>();
        await reconciliarArquivos.ExecutarAsync();
    }

    private async Task<Arquivo> RecarregarAsync(Guid id)
    {
        using var escopo = _factory.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ArquivosDbContext>();
        return (await contexto.Arquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id))!;
    }

    public void Dispose() => _factory.Dispose();
}
