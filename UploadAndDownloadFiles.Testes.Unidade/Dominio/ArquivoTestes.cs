using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Dominio.Excecoes;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Testes.Unidade.Dominio;

public class ArquivoTestes
{
    private const long _mb = 1024 * 1024;

    [Fact]
    public void Registrar_ComTamanhoMenorQue100MB_UsaModoPutUnicoENaoCalculaPartes()
    {
        var arquivo = Arquivo.Registrar("foto.png", 50 * _mb);

        Assert.Equal(ModoUpload.PutUnico, arquivo.Modo);
        Assert.Equal(StatusArquivo.Pendente, arquivo.Status);
        Assert.Null(arquivo.TamanhoParte);
        Assert.Null(arquivo.QuantidadePartesEsperada);
        Assert.Null(arquivo.IdUploadS3);
        Assert.Contains(arquivo.Id.ToString(), arquivo.Chave);
    }

    [Fact]
    public void Registrar_ComTamanhoMaiorOuIgualA100MB_UsaModoMultipartECalculaPartes()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 100 * _mb);

        Assert.Equal(ModoUpload.Multipart, arquivo.Modo);
        Assert.Equal(StatusArquivo.Pendente, arquivo.Status);
        Assert.NotNull(arquivo.TamanhoParte);
        Assert.NotNull(arquivo.QuantidadePartesEsperada);
    }

    [Fact]
    public void Registrar_GeraChaveInternamente_IgnorandoQualquerEntradaExterna()
    {
        var arquivo1 = Arquivo.Registrar("mesmo-nome.txt", 10 * _mb);
        var arquivo2 = Arquivo.Registrar("mesmo-nome.txt", 10 * _mb);

        Assert.NotEqual(arquivo1.Chave, arquivo2.Chave);
        Assert.NotEqual(arquivo1.Id, arquivo2.Id);
    }

    [Theory]
    [InlineData(100L * 1024 * 1024)]
    [InlineData(500L * 1024 * 1024)]
    [InlineData(5L * 1024 * 1024 * 1024)]
    [InlineData(5L * 1024 * 1024 * 1024 * 1024)]
    public void CalcularParticionamento_RespeitaPisoDe100MBELimiteDe10000Partes(long tamanhoDeclarado)
    {
        var (tamanhoParte, quantidadePartesEsperada) = Arquivo.CalcularParticionamento(tamanhoDeclarado);

        Assert.True(tamanhoParte >= Arquivo.TamanhoMinimoParteEmBytes);
        Assert.True(quantidadePartesEsperada <= Arquivo.LimiteMaximoDePartes);
        Assert.True(quantidadePartesEsperada >= 1);
    }

    [Fact]
    public void IniciarMultipart_APartirDePendenteEmModoMultipart_TransicionaParaEnviando()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);

        arquivo.IniciarMultipart("upload-id-123");

        Assert.Equal(StatusArquivo.Enviando, arquivo.Status);
        Assert.Equal("upload-id-123", arquivo.IdUploadS3);
    }

    [Fact]
    public void IniciarMultipart_EmModoPutUnico_LancaTransicaoInvalida()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * _mb);

        Assert.Throws<TransicaoInvalidaException>(() => arquivo.IniciarMultipart("upload-id-123"));
    }

    [Fact]
    public void IniciarMultipart_QuandoJaEmEnviando_LancaTransicaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        Assert.Throws<TransicaoInvalidaException>(() => arquivo.IniciarMultipart("outro-id"));
    }

    [Fact]
    public void Finalizar_APartirDeEnviando_TransicionaParaCompletoEGravaTamanhoReal()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        arquivo.Finalizar(200 * _mb);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(200 * _mb, arquivo.TamanhoReal);
    }

    [Fact]
    public void Finalizar_ChamadoDuasVezes_EIdempotente()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        arquivo.Finalizar(200 * _mb);
        arquivo.Finalizar(200 * _mb);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(200 * _mb, arquivo.TamanhoReal);
    }

    [Fact]
    public void Finalizar_APartirDePendente_LancaTransicaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);

        Assert.Throws<TransicaoInvalidaException>(() => arquivo.Finalizar(200 * _mb));
    }

    [Fact]
    public void ReconciliarComoCompleto_APartirDePendente_TransicionaParaCompleto()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * _mb);

        arquivo.ReconciliarComoCompleto(10 * _mb);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
        Assert.Equal(10 * _mb, arquivo.TamanhoReal);
    }

    [Fact]
    public void ReconciliarComoCompleto_APartirDeEnviando_TransicionaParaCompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        arquivo.ReconciliarComoCompleto(200 * _mb);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
    }

    [Fact]
    public void ReconciliarComoCompleto_QuandoJaCompleto_EIdempotente()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * _mb);
        arquivo.ReconciliarComoCompleto(10 * _mb);

        arquivo.ReconciliarComoCompleto(10 * _mb);

        Assert.Equal(StatusArquivo.Completo, arquivo.Status);
    }

    [Fact]
    public void MarcarIncompleto_APartirDeEnviando_TransicionaParaIncompleto()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        arquivo.MarcarIncompleto();

        Assert.Equal(StatusArquivo.Incompleto, arquivo.Status);
    }

    [Fact]
    public void MarcarIncompleto_APartirDePendente_LancaTransicaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);

        Assert.Throws<TransicaoInvalidaException>(() => arquivo.MarcarIncompleto());
    }

    [Fact]
    public void MarcarInvalido_APartirDePendente_TransicionaParaInvalido()
    {
        var arquivo = Arquivo.Registrar("foto.png", 10 * _mb);

        arquivo.MarcarInvalido();

        Assert.Equal(StatusArquivo.Inválido, arquivo.Status);
    }

    [Fact]
    public void MarcarInvalido_APartirDeEnviando_LancaTransicaoInvalida()
    {
        var arquivo = Arquivo.Registrar("video.mp4", 200 * _mb);
        arquivo.IniciarMultipart("upload-id-123");

        Assert.Throws<TransicaoInvalidaException>(() => arquivo.MarcarInvalido());
    }
}
