using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;

namespace UploadAndDownloadFiles.Infraestrutura.Aws;

public sealed class ArmazenamentoObjetosS3(IAmazonS3 s3, IOptions<OpcoesArmazenamentoS3> opcoes) : IArmazenamentoObjetos
{
    private static readonly TimeSpan s_expiracaoUrlPreAssinada = TimeSpan.FromHours(1);

    private readonly IAmazonS3 _s3 = s3;
    private readonly string _nomeBucket = opcoes.Value.NomeBucket;

    public async Task<UrlDeUploadUnico> CriarUrlDeUploadUnicoAsync(string chave, long tamanho, string nomeOriginal, CancellationToken cancellationToken = default)
    {
        var contentDisposition = CabecalhoContentDisposition.Montar(nomeOriginal);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _nomeBucket,
            Key = chave,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(s_expiracaoUrlPreAssinada),
        };
        request.Headers.ContentLength = tamanho;
        request.Headers.ContentDisposition = contentDisposition;

        var url = await _s3.GetPreSignedURLAsync(request);

        return new UrlDeUploadUnico(url, contentDisposition);
    }

    public async Task<string> IniciarMultipartAsync(string chave, string nomeOriginal, CancellationToken cancellationToken = default)
    {
        var request = new InitiateMultipartUploadRequest
        {
            BucketName = _nomeBucket,
            Key = chave,
        };
        request.Headers.ContentDisposition = CabecalhoContentDisposition.Montar(nomeOriginal);

        var response = await _s3.InitiateMultipartUploadAsync(request, cancellationToken);

        return response.UploadId;
    }

    public Task<string> CriarUrlDeParteAsync(string chave, string idUploadS3, int numeroParte, long tamanhoParte, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _nomeBucket,
            Key = chave,
            Verb = HttpVerb.PUT,
            UploadId = idUploadS3,
            PartNumber = numeroParte,
            Expires = DateTime.UtcNow.Add(s_expiracaoUrlPreAssinada),
        };
        request.Headers.ContentLength = tamanhoParte;

        return _s3.GetPreSignedURLAsync(request);
    }

    public async Task<IReadOnlyList<ParteEnviada>> ListarPartesEnviadasAsync(string chave, string idUploadS3, CancellationToken cancellationToken = default)
    {
        var partes = new List<ParteEnviada>();
        string? marcadorDeParte = null;

        do
        {
            var response = await _s3.ListPartsAsync(new ListPartsRequest
            {
                BucketName = _nomeBucket,
                Key = chave,
                UploadId = idUploadS3,
                PartNumberMarker = marcadorDeParte,
            }, cancellationToken);

            partes.AddRange((response.Parts ?? [])
                .Where(p => p.PartNumber.HasValue)
                .Select(p => new ParteEnviada(p.PartNumber!.Value, p.ETag)));

            marcadorDeParte = response.IsTruncated == true ? response.NextPartNumberMarker?.ToString() : null;
        } while (marcadorDeParte is not null);

        return partes;
    }

    public async Task CompletarMultipartAsync(string chave, string idUploadS3, IReadOnlyList<ParteEnviada> partes, CancellationToken cancellationToken = default)
    {
        var request = new CompleteMultipartUploadRequest
        {
            BucketName = _nomeBucket,
            Key = chave,
            UploadId = idUploadS3,
            PartETags = [.. partes.Select(p => new PartETag(p.Numero, p.ETag))],
        };

        try
        {
            await _s3.CompleteMultipartUploadAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchUpload")
        {
            // Idempotência: o upload já foi completado anteriormente (uploadId não existe mais).
            var tamanho = await ObterTamanhoObjetoAsync(chave, cancellationToken);
            if (tamanho is null)
                throw;
        }
    }

    public Task AbortarMultipartAsync(string chave, string idUploadS3, CancellationToken cancellationToken = default) =>
        _s3.AbortMultipartUploadAsync(_nomeBucket, chave, idUploadS3, cancellationToken);

    public async Task<long?> ObterTamanhoObjetoAsync(string chave, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3.GetObjectMetadataAsync(_nomeBucket, chave, cancellationToken);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
