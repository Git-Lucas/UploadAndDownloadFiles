using UploadAndDownloadFiles.Aplicacao.CasosDeUso.PutUnico;

namespace UploadAndDownloadFiles.Api.Endpoints.PutUnico;

/// <summary>Endpoints exclusivos do fluxo de PUT único, sob o prefixo "put-unico".</summary>
public static class PutUnicoEndpoints
{
    public static void MapPutUnicoEndpoints(this IEndpointRouteBuilder grupo)
    {
        var grupoPutUnico = grupo.MapGroup("/put-unico");

        grupoPutUnico.MapPost("/{id:guid}/confirmar", ConfirmarAsync);
    }

    private static async Task<IResult> ConfirmarAsync(
        Guid id,
        ConfirmarUploadUnico casoDeUso,
        CancellationToken cancellationToken)
    {
        await casoDeUso.ExecutarAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
