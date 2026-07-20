using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;

public sealed class ListarPartesFaltantes
{
    private readonly IRepositorioArquivos _repositorio;
    private readonly IArmazenamentoObjetos _armazenamento;

    public ListarPartesFaltantes(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
    {
        _repositorio = repositorio;
        _armazenamento = armazenamento;
    }

    public async Task<IReadOnlyList<int>> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Modo != ModoUpload.Multipart || arquivo.Status != StatusArquivo.Enviando)
            throw new OperacaoInvalidaException($"Arquivo '{id}' não está em upload multipart.");

        var partesEnviadas = await _armazenamento.ListarPartesEnviadasAsync(arquivo.Chave, arquivo.IdUploadS3!, cancellationToken);
        var numerosEnviados = partesEnviadas.Select(p => p.Numero).ToHashSet();

        return Enumerable.Range(1, arquivo.QuantidadePartesEsperada!.Value)
            .Where(numero => !numerosEnviados.Contains(numero))
            .ToList();
    }
}
