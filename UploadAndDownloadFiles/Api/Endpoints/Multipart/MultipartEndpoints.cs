using UploadAndDownloadFiles.Aplicacao.CasosDeUso.Multipart;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Shared.Dtos;

namespace UploadAndDownloadFiles.Api.Endpoints.Multipart;

/// <summary>Endpoints exclusivos do fluxo multipart, sob o prefixo "multipart".</summary>
public static class MultipartEndpoints
{
    public static void MapMultipartEndpoints(this IEndpointRouteBuilder grupo)
    {
        var grupoMultipart = grupo.MapGroup("/multipart");

        grupoMultipart.MapGet("/{id:guid}/partes/faltantes", ListarPartesFaltantesAsync);
        grupoMultipart.MapGet("/{id:guid}/partes/{numero:int}/url", ObterUrlDeParteAsync);
        grupoMultipart.MapPost("/{id:guid}/finalizar", FinalizarAsync);
    }

    private static async Task<PartesFaltantesResponse> ListarPartesFaltantesAsync(
        Guid id,
        ListarPartesFaltantes casoDeUso,
        CancellationToken cancellationToken)
    {
        var faltantes = await casoDeUso.ExecutarAsync(id, cancellationToken);
        return new PartesFaltantesResponse(faltantes);
    }

    private static async Task<UrlParteResponse> ObterUrlDeParteAsync(
        Guid id,
        int numero,
        ObterUrlDeParte casoDeUso,
        CancellationToken cancellationToken)
    {
        var url = await casoDeUso.ExecutarAsync(id, numero, cancellationToken);
        return new UrlParteResponse(numero, url);
    }

    private static async Task<IResult> FinalizarAsync(
        Guid id,
        FinalizarUploadRequest requisicao,
        FinalizarUpload casoDeUso,
        CancellationToken cancellationToken)
    {
        var partes = requisicao.Partes.Select(p => new ParteEnviada(p.Numero, p.ETag)).ToList();
        await casoDeUso.ExecutarAsync(id, partes, cancellationToken);
        return Results.NoContent();
    }
}
