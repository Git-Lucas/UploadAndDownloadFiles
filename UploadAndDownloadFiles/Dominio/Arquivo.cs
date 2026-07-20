using UploadAndDownloadFiles.Dominio.Excecoes;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Dominio;

/// <summary>
/// Agregado raiz do upload/download de arquivos. Encapsula a máquina de 5 estados
/// (Pendente, Enviando, Completo, Incompleto, Inválido) e o cálculo de particionamento multipart.
/// </summary>
public sealed class Arquivo
{
    public const long TamanhoMinimoParteEmBytes = 100L * 1024 * 1024;
    public const long LimiarMultipartEmBytes = TamanhoMinimoParteEmBytes;
    public const int LimiteMaximoDePartes = 10_000;
    private const int DivisorAlvoDeParticionamento = 9500;

    public Guid Id { get; private set; }
    public string Chave { get; private set; } = string.Empty;
    public string NomeOriginal { get; private set; } = string.Empty;
    public long TamanhoDeclarado { get; private set; }
    public string? IdUploadS3 { get; private set; }
    public StatusArquivo Status { get; private set; }
    public long? TamanhoParte { get; private set; }
    public int? QuantidadePartesEsperada { get; private set; }
    public long? TamanhoReal { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    public ModoUpload Modo => TamanhoDeclarado >= LimiarMultipartEmBytes ? ModoUpload.Multipart : ModoUpload.PutUnico;

    private Arquivo()
    {
        // Uso exclusivo do EF Core para materialização.
    }

    private Arquivo(Guid id, string chave, string nomeOriginal, long tamanhoDeclarado, DateTime agora)
    {
        Id = id;
        Chave = chave;
        NomeOriginal = nomeOriginal;
        TamanhoDeclarado = tamanhoDeclarado;
        Status = StatusArquivo.Pendente;
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public static Arquivo Registrar(string nomeOriginal, long tamanhoDeclarado)
    {
        if (string.IsNullOrWhiteSpace(nomeOriginal))
            throw new ArgumentException("Nome do arquivo é obrigatório.", nameof(nomeOriginal));
        if (tamanhoDeclarado <= 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoDeclarado), "Tamanho declarado deve ser maior que zero.");

        var id = Guid.NewGuid();
        var chave = $"{id}/{nomeOriginal}";
        var agora = DateTime.UtcNow;

        var arquivo = new Arquivo(id, chave, nomeOriginal, tamanhoDeclarado, agora);

        if (arquivo.Modo == ModoUpload.Multipart)
        {
            var (tamanhoParte, quantidadePartesEsperada) = CalcularParticionamento(tamanhoDeclarado);
            arquivo.TamanhoParte = tamanhoParte;
            arquivo.QuantidadePartesEsperada = quantidadePartesEsperada;
        }

        return arquivo;
    }

    /// <summary>
    /// TamanhoParte = max(100MB, arredondaCima(tamanho/9500)); garante que a quantidade de
    /// partes nunca excede o limite de 10.000 do S3 para arquivos de até 5 TB.
    /// </summary>
    public static (long TamanhoParte, int QuantidadePartesEsperada) CalcularParticionamento(long tamanhoDeclarado)
    {
        if (tamanhoDeclarado <= 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoDeclarado), "Tamanho declarado deve ser maior que zero.");

        var tamanhoParteAlvo = DivisaoComArredondamentoParaCima(tamanhoDeclarado, DivisorAlvoDeParticionamento);
        var tamanhoParte = Math.Max(TamanhoMinimoParteEmBytes, tamanhoParteAlvo);
        var quantidadePartesEsperada = (int)DivisaoComArredondamentoParaCima(tamanhoDeclarado, tamanhoParte);

        return (tamanhoParte, quantidadePartesEsperada);
    }

    private static long DivisaoComArredondamentoParaCima(long dividendo, long divisor) => (dividendo + divisor - 1) / divisor;

    public void IniciarMultipart(string idUploadS3)
    {
        if (string.IsNullOrWhiteSpace(idUploadS3))
            throw new ArgumentException("IdUploadS3 é obrigatório.", nameof(idUploadS3));
        if (Modo != ModoUpload.Multipart)
            throw new TransicaoInvalidaException("Só é possível iniciar multipart para arquivos no modo Multipart.");
        if (Status != StatusArquivo.Pendente)
            throw new TransicaoInvalidaException($"Não é possível iniciar multipart a partir do status {Status}.");

        IdUploadS3 = idUploadS3;
        Status = StatusArquivo.Enviando;
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>Idempotente: se já Completo, não faz nada.</summary>
    public void Finalizar(long tamanhoReal)
    {
        if (Status == StatusArquivo.Completo)
            return;
        if (Status != StatusArquivo.Enviando)
            throw new TransicaoInvalidaException($"Não é possível finalizar a partir do status {Status}.");

        TamanhoReal = tamanhoReal;
        Status = StatusArquivo.Completo;
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>Usado pela reconciliação quando o objeto já existe no S3. Idempotente.</summary>
    public void ReconciliarComoCompleto(long tamanhoReal)
    {
        if (Status == StatusArquivo.Completo)
            return;
        if (Status != StatusArquivo.Pendente && Status != StatusArquivo.Enviando)
            throw new TransicaoInvalidaException($"Não é possível reconciliar como completo a partir do status {Status}.");

        TamanhoReal = tamanhoReal;
        Status = StatusArquivo.Completo;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void MarcarIncompleto()
    {
        if (Status != StatusArquivo.Enviando)
            throw new TransicaoInvalidaException($"Não é possível marcar como incompleto a partir do status {Status}.");

        Status = StatusArquivo.Incompleto;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void MarcarInvalido()
    {
        if (Status != StatusArquivo.Pendente)
            throw new TransicaoInvalidaException($"Não é possível marcar como inválido a partir do status {Status}.");

        Status = StatusArquivo.Inválido;
        AtualizadoEm = DateTime.UtcNow;
    }
}
