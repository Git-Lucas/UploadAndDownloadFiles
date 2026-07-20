using UploadAndDownloadFiles.Api.Endpoints.Multipart;
using UploadAndDownloadFiles.Api.Endpoints.PutUnico;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Shared.Dtos;

namespace UploadAndDownloadFiles.Api.Endpoints;

/// <summary>
/// Endpoints comuns aos dois fluxos de upload (registrar, que decide o modo, e download).
/// Os endpoints específicos de cada fluxo ficam em <see cref="PutUnicoEndpoints"/> (PUT único)
/// e <see cref="MultipartEndpoints"/> (multipart), sob os prefixos "put-unico" e "multipart".
/// </summary>
public static class ArquivosEndpoints
{
    public static void MapArquivosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/arquivos");

        grupo.MapPost("/", RegistrarAsync);
        grupo.MapGet("/{id:guid}/download", ObterDownloadAsync);

        grupo.MapPutUnicoEndpoints();
        grupo.MapMultipartEndpoints();
    }

    private static async Task<RegistrarArquivoResponse> RegistrarAsync(
        RegistrarArquivoRequest requisicao,
        RegistrarArquivo casoDeUso,
        CancellationToken cancellationToken)
    {
        var resultado = await casoDeUso.ExecutarAsync(requisicao.NomeArquivo, requisicao.TamanhoDeclarado, cancellationToken);

        return new RegistrarArquivoResponse(
            resultado.Id,
            resultado.Modo,
            resultado.UrlUpload,
            resultado.CabecalhoContentDisposition,
            resultado.TamanhoParte,
            resultado.QuantidadePartesEsperada);
    }

    private static async Task<DownloadResponse> ObterDownloadAsync(
        Guid id,
        ObterDownload casoDeUso,
        CancellationToken cancellationToken)
    {
        var url = await casoDeUso.ExecutarAsync(id, cancellationToken);
        return new DownloadResponse(url);
    }
}
