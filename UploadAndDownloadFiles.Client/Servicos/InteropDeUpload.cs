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

    public async Task EnviarArquivoCompletoAsync(string idInputArquivo, string url, string cabecalhoContentDisposition, DotNetObjectReference<RelatorDeProgresso> relatorProgresso)
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("enviarArquivoCompleto", idInputArquivo, url, cabecalhoContentDisposition, relatorProgresso);
    }

    public async Task<string> EnviarParteAsync(string idInputArquivo, int numeroParte, long tamanhoParte, string url, DotNetObjectReference<RelatorDeProgresso> relatorProgresso)
    {
        var modulo = await _moduloJs.Value;
        return await modulo.InvokeAsync<string>("enviarParte", idInputArquivo, numeroParte, tamanhoParte, url, relatorProgresso);
    }

    public async Task IniciarDownloadAsync(string url)
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("iniciarDownload", url);
    }

    /// <summary>
    /// Passa a monitorar a conectividade real, notificando <paramref name="referencia"/> (método
    /// <c>AtualizarConexao</c>) a cada mudança. Combina os eventos <c>online</c>/<c>offline</c> do
    /// browser com uma sondagem periódica (heartbeat) a <paramref name="urlHeartbeat"/>, executada
    /// a cada <paramref name="periodoMs"/> milissegundos. Retorna o estado inicial de
    /// <c>navigator.onLine</c>.
    /// </summary>
    public async Task<bool> IniciarMonitorConexaoAsync<T>(DotNetObjectReference<T> referencia, string urlHeartbeat, int periodoMs) where T : class
    {
        var modulo = await _moduloJs.Value;
        return await modulo.InvokeAsync<bool>("iniciarMonitorConexao", referencia, urlHeartbeat, periodoMs);
    }

    /// <summary>Dispara uma sondagem de conectividade imediata, sem esperar o próximo heartbeat.</summary>
    public async Task SondarAgoraAsync()
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("sondarAgora");
    }

    public async Task PararMonitorConexaoAsync()
    {
        var modulo = await _moduloJs.Value;
        await modulo.InvokeVoidAsync("pararMonitorConexao");
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
