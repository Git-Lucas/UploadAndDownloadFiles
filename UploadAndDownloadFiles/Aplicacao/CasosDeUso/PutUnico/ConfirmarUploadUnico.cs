using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso.PutUnico;

/// <summary>
/// Confirma um upload PUT único logo após o envio, evitando esperar pela reconciliação
/// periódica: verifica via `HeadObject` se o objeto já existe no S3 e, se sim, marca o
/// registro como `Completo`. Idempotente.
/// </summary>
public sealed class ConfirmarUploadUnico(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arquivo = await _repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new ArquivoNaoEncontradoException(id);

        if (arquivo.Status == StatusArquivo.Completo)
            return;

        if (arquivo.Modo != ModoUpload.PutUnico || arquivo.Status != StatusArquivo.Pendente)
            throw new OperacaoInvalidaException($"Arquivo '{id}' não está em upload de PUT único pendente de confirmação.");

        var tamanhoReal = await _armazenamento.ObterTamanhoObjetoAsync(arquivo.Chave, cancellationToken)
            ?? throw new OperacaoInvalidaException($"Objeto '{arquivo.Chave}' ainda não encontrado no S3.");

        arquivo.ReconciliarComoCompleto(tamanhoReal);
        await _repositorio.SalvarAlteracoesAsync(cancellationToken);
    }
}
