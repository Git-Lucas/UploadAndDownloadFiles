using System.Net;
using System.Net.Http.Json;
using Moq;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;
using UploadAndDownloadFiles.Shared.Dtos;

namespace UploadAndDownloadFiles.Testes.Integracao;

public class ArquivosApiTestes : IDisposable
{
    private const long Mb = 1024 * 1024;

    private readonly ArquivosWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ArquivosApiTestes()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FluxoCompletoMultipart_RegistrarUrlPartesFaltantesFinalizarDownload()
    {
        _factory.MockArmazenamento
            .Setup(a => a.IniciarMultipartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("upload-id-teste");

        var respostaRegistro = await _client.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest("video.mp4", 200 * Mb));
        respostaRegistro.EnsureSuccessStatusCode();
        var registro = await respostaRegistro.Content.ReadFromJsonAsync<RegistrarArquivoResponse>();

        Assert.NotNull(registro);
        Assert.Equal(ModoUpload.Multipart, registro!.Modo);
        Assert.Null(registro.UrlUpload);
        Assert.NotNull(registro.TamanhoParte);
        Assert.NotNull(registro.QuantidadePartesEsperada);

        _factory.MockArmazenamento
            .Setup(a => a.CriarUrlDeParteAsync(It.IsAny<string>(), "upload-id-teste", 1, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.exemplo/url-parte-1");

        var respostaUrl = await _client.GetAsync($"/api/arquivos/multipart/{registro.Id}/partes/1/url");
        respostaUrl.EnsureSuccessStatusCode();
        var urlParte = await respostaUrl.Content.ReadFromJsonAsync<UrlParteResponse>();
        Assert.Equal("https://s3.exemplo/url-parte-1", urlParte!.Url);

        var totalPartes = registro.QuantidadePartesEsperada!.Value;
        _factory.MockArmazenamento
            .Setup(a => a.ListarPartesEnviadasAsync(It.IsAny<string>(), "upload-id-teste", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "etag-1")]);

        var respostaFaltantes = await _client.GetAsync($"/api/arquivos/multipart/{registro.Id}/partes/faltantes");
        respostaFaltantes.EnsureSuccessStatusCode();
        var faltantes = await respostaFaltantes.Content.ReadFromJsonAsync<PartesFaltantesResponse>();
        Assert.Equal(Enumerable.Range(2, totalPartes - 1), faltantes!.NumerosFaltantes);

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(200 * Mb);

        var etags = Enumerable.Range(1, totalPartes).Select(n => new ParteEtag(n, $"etag-{n}")).ToList();
        var respostaFinalizar = await _client.PostAsJsonAsync($"/api/arquivos/multipart/{registro.Id}/finalizar", new FinalizarUploadRequest(etags));
        Assert.Equal(HttpStatusCode.NoContent, respostaFinalizar.StatusCode);

        _factory.MockAssinadorCdn
            .Setup(a => a.GerarUrlAssinada(It.IsAny<string>()))
            .Returns("https://cdn.exemplo/download-assinado");

        var respostaDownload = await _client.GetAsync($"/api/arquivos/{registro.Id}/download");
        respostaDownload.EnsureSuccessStatusCode();
        var download = await respostaDownload.Content.ReadFromJsonAsync<DownloadResponse>();
        Assert.Equal("https://cdn.exemplo/download-assinado", download!.Url);
    }

    [Fact]
    public async Task RegistrarArquivoPequeno_RetornaUrlUnicaDePutSemMultipart()
    {
        _factory.MockArmazenamento
            .Setup(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), 10 * Mb, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.exemplo/url-unica");

        var resposta = await _client.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest("foto.png", 10 * Mb));
        resposta.EnsureSuccessStatusCode();
        var registro = await resposta.Content.ReadFromJsonAsync<RegistrarArquivoResponse>();

        Assert.Equal(ModoUpload.PutUnico, registro!.Modo);
        Assert.Equal("https://s3.exemplo/url-unica", registro.UrlUpload);

        _factory.MockArmazenamento.Verify(a => a.IniciarMultipartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmarPutUnico_ComObjetoNoS3_MarcaCompletoELiberaDownload()
    {
        _factory.MockArmazenamento
            .Setup(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), 10 * Mb, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.exemplo/url-unica");

        var respostaRegistro = await _client.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest("foto.png", 10 * Mb));
        var registro = await respostaRegistro.Content.ReadFromJsonAsync<RegistrarArquivoResponse>();

        _factory.MockArmazenamento
            .Setup(a => a.ObterTamanhoObjetoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10 * Mb);

        var respostaConfirmar = await _client.PostAsync($"/api/arquivos/put-unico/{registro!.Id}/confirmar", content: null);
        Assert.Equal(HttpStatusCode.NoContent, respostaConfirmar.StatusCode);

        _factory.MockAssinadorCdn
            .Setup(a => a.GerarUrlAssinada(It.IsAny<string>()))
            .Returns("https://cdn.exemplo/download-assinado");

        var respostaDownload = await _client.GetAsync($"/api/arquivos/{registro.Id}/download");
        respostaDownload.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ConfirmarPutUnico_ComObjetoAindaAusenteNoS3_Retorna409()
    {
        _factory.MockArmazenamento
            .Setup(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), 10 * Mb, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.exemplo/url-unica");

        var respostaRegistro = await _client.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest("foto.png", 10 * Mb));
        var registro = await respostaRegistro.Content.ReadFromJsonAsync<RegistrarArquivoResponse>();

        var respostaConfirmar = await _client.PostAsync($"/api/arquivos/put-unico/{registro!.Id}/confirmar", content: null);

        Assert.Equal(HttpStatusCode.Conflict, respostaConfirmar.StatusCode);
    }

    [Fact]
    public async Task Download_DeArquivoInexistente_Retorna404()
    {
        var resposta = await _client.GetAsync($"/api/arquivos/{Guid.NewGuid()}/download");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Download_DeArquivoAindaNaoCompleto_Retorna409()
    {
        _factory.MockArmazenamento
            .Setup(a => a.CriarUrlDeUploadUnicoAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.exemplo/url-unica");

        var respostaRegistro = await _client.PostAsJsonAsync("/api/arquivos", new RegistrarArquivoRequest("foto.png", 10 * Mb));
        var registro = await respostaRegistro.Content.ReadFromJsonAsync<RegistrarArquivoResponse>();

        var respostaDownload = await _client.GetAsync($"/api/arquivos/{registro!.Id}/download");

        Assert.Equal(HttpStatusCode.Conflict, respostaDownload.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
