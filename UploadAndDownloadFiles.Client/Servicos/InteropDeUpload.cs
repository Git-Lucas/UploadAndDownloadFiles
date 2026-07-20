using Microsoft.JSInterop;

namespace UploadAndDownloadFiles.Client.Servicos;

/// <summary>
/// Wrapper do módulo JS `uploadInterop.js`. Os bytes do arquivo nunca passam pelo runtime .NET:
/// o JS lê o `File` diretamente do input, fatia com `Blob.slice` e envia via `fetch`.
/// </summary>
public sealed class InteropDeUpload(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduloJs = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/uploadInterop.js").AsTask());

    public async Task<long> ObterTamanhoArquivoAsync(string idInputArquivo)
    {
        var modulo = await _moduloJs.Value;
        return await modulo.InvokeAsync<long>("obterTamanhoArquivo", idInputArquivo);
    }

    public async Task<string?> ObterNomeArquivoAsync(string idInputArquivo)
    {
        var modulo = await _moduloJs.Value;
        return await modulo.InvokeAsync<string?>("obterNomeArquivo", idInputArquivo);
    }

    public async Task EnviarArquivoCompletoAsync(string idInputArquivo, string url)
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("enviarArquivoCompleto", idInputArquivo, url);
    }

    public async Task<string> EnviarParteAsync(string idInputArquivo, int numeroParte, long tamanhoParte, string url)
    {
        var modulo = await _moduloJs.Value;
        return await modulo.InvokeAsync<string>("enviarParte", idInputArquivo, numeroParte, tamanhoParte, url);
    }

    public async Task IniciarDownloadAsync(string url)
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("iniciarDownload", url);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduloJs.IsValueCreated)
        {
            var modulo = await _moduloJs.Value;
            await modulo.DisposeAsync();
        }
    }
}
