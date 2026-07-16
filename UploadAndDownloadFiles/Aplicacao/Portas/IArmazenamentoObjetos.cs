using UploadAndDownloadFiles.Aplicacao.Modelos;

namespace UploadAndDownloadFiles.Aplicacao.Portas;

/// <summary>Porta para o armazenamento de objetos (S3).</summary>
public interface IArmazenamentoObjetos
{
    /// <summary>URL pré-assinada de PUT único, com `Content-Length-Range` igual ao tamanho declarado.</summary>
    Task<string> CriarUrlDeUploadUnicoAsync(string chave, long tamanho, CancellationToken cancellationToken = default);

    /// <summary>Inicia o multipart upload no S3 e retorna o `uploadId`.</summary>
    Task<string> IniciarMultipartAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>URL pré-assinada de uma parte, sob demanda (também usada para reassinatura).</summary>
    Task<string> CriarUrlDeParteAsync(string chave, string idUploadS3, int numeroParte, long tamanhoParte, CancellationToken cancellationToken = default);

    /// <summary>Partes já aceitas pelo S3 (`ListParts`), fonte de verdade para retomada e reconciliação.</summary>
    Task<IReadOnlyList<ParteEnviada>> ListarPartesEnviadasAsync(string chave, string idUploadS3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conclui o multipart upload (`CompleteMultipartUpload`). Deve ser tolerante a chamadas repetidas:
    /// se o `uploadId` já não existir mas o objeto já estiver completo no S3, deve retornar normalmente.
    /// </summary>
    Task CompletarMultipartAsync(string chave, string idUploadS3, IReadOnlyList<ParteEnviada> partes, CancellationToken cancellationToken = default);

    Task AbortarMultipartAsync(string chave, string idUploadS3, CancellationToken cancellationToken = default);

    /// <summary>`HeadObject`: tamanho real do objeto, ou `null` se o objeto não existir.</summary>
    Task<long?> ObterTamanhoObjetoAsync(string chave, CancellationToken cancellationToken = default);
}
