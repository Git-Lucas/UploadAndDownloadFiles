using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;

/// <summary>Finaliza um upload multipart a partir dos ETags das partes. Idempotente.</summary>
public sealed class FinalizarUpload(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task ExecutarAsync(Guid id, IReadOnlyList<ParteEnviada> partes, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Status == StatusArquivo.Completo)
            return;

        if (arquivo.Modo != ModoUpload.Multipart || arquivo.Status != StatusArquivo.Enviando)
            throw new OperacaoInvalidaException($"Arquivo '{id}' não está em upload multipart.");

        await _armazenamento.CompletarMultipartAsync(arquivo.Chave, arquivo.IdUploadS3!, partes, cancellationToken);

        var tamanhoReal = await _armazenamento.ObterTamanhoObjetoAsync(arquivo.Chave, cancellationToken)
            ?? throw new OperacaoInvalidaException($"Objeto '{arquivo.Chave}' não encontrado no S3 após a finalização.");

        arquivo.Finalizar(tamanhoReal);
        await _repositorio.SalvarAlteracoesAsync(cancellationToken);
    }
}
