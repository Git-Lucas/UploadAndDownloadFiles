using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UploadAndDownloadFiles.Aplicacao.Excecoes;
using UploadAndDownloadFiles.Dominio.Excecoes;

namespace UploadAndDownloadFiles.Api;

/// <summary>Traduz exceções de Domínio/Aplicação em respostas HTTP (`ProblemDetails`).</summary>
public sealed class ManipuladorDeExcecoes : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            ArquivoNaoEncontradoException => (StatusCodes.Status404NotFound, "Arquivo não encontrado"),
            OperacaoInvalidaException => (StatusCodes.Status409Conflict, "Operação inválida"),
            TransicaoInvalidaException => (StatusCodes.Status409Conflict, "Transição de estado inválida"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            _ => (0, string.Empty),
        };

        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = titulo, Detail = exception.Message },
            cancellationToken);

        return true;
    }
}
