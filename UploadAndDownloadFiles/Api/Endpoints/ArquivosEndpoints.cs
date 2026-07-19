using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Shared.Dtos;

namespace UploadAndDownloadFiles.Api.Endpoints;

/// <summary>Endpoints finos: apenas mapeiam DTO do `Shared` ↔ caso de uso, sem lógica de negócio.</summary>
public static class ArquivosEndpoints
{
    public static void MapArquivosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/arquivos");

        grupo.MapPost("/", RegistrarAsync);
        grupo.MapGet("/{id:guid}/partes/faltantes", ListarPartesFaltantesAsync);
        grupo.MapGet("/{id:guid}/partes/{numero:int}/url", ObterUrlDeParteAsync);
        grupo.MapPost("/{id:guid}/finalizar", FinalizarAsync);
        grupo.MapPost("/{id:guid}/confirmar", ConfirmarAsync);
        grupo.MapGet("/{id:guid}/download", ObterDownloadAsync);
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
            resultado.TamanhoParte,
            resultado.QuantidadePartesEsperada);
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

    private static async Task<IResult> ConfirmarAsync(
        Guid id,
        ConfirmarUploadUnico casoDeUso,
        CancellationToken cancellationToken)
    {
        await casoDeUso.ExecutarAsync(id, cancellationToken);
        return Results.NoContent();
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
