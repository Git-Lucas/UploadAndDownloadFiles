using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;

/// <summary>
/// Lista as partes que ainda faltam enviar num upload multipart. Idempotente: se o arquivo já
/// está <see cref="StatusArquivo.Completo"/> (ex.: botão "Tentar novamente" após concluir),
/// não há partes faltantes e retorna lista vazia, em vez de falhar.
/// </summary>
public sealed class ListarPartesFaltantes(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task<IReadOnlyList<int>> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Status == StatusArquivo.Completo)
            return [];

        if (arquivo.Modo != ModoUpload.Multipart || arquivo.Status != StatusArquivo.Enviando)
            throw new OperacaoInvalidaException($"Arquivo '{id}' não está em upload multipart.");

        var partesEnviadas = await _armazenamento.ListarPartesEnviadasAsync(arquivo.Chave, arquivo.IdUploadS3!, cancellationToken);
        var numerosEnviados = partesEnviadas.Select(p => p.Numero).ToHashSet();

        return [.. Enumerable.Range(1, arquivo.QuantidadePartesEsperada!.Value).Where(numero => !numerosEnviados.Contains(numero))];
    }
}
