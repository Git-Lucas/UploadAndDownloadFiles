using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace UploadAndDownloadFiles.Client.Servicos;

/// <summary>
/// Agrega o progresso de bytes de vários envios concorrentes (o arquivo inteiro no PUT único ou
/// cada parte no multipart) num único percentual e o repassa à UI. O JS reporta, por slot, o total
/// de bytes já carregados via <see cref="Reportar"/>; a soma dos slots dividida pelo tamanho total
/// dá o percentual. Como cada evento traz o acumulado (e não um incremento), uma retentativa que
/// recomeça do zero simplesmente sobrescreve o slot, corrigindo o total sozinha.
/// </summary>
public sealed class RelatorDeProgresso(long bytesTotais, Action<double> aoAtualizar)
{
    private readonly ConcurrentDictionary<int, long> _bytesPorSlot = new();

    /// <summary>
    /// Marca um slot como já concluído com o número de bytes informado, sem notificar a UI. Usado
    /// para contabilizar, no início de uma retomada, as partes que o servidor já recebeu.
    /// </summary>
    public void Semear(int slot, long bytes) => _bytesPorSlot[slot] = bytes;

    [JSInvokable]
    public void Reportar(int slot, long bytesCarregados)
    {
        _bytesPorSlot[slot] = bytesCarregados;
        Notificar();
    }

    public void Notificar()
    {
        var enviados = _bytesPorSlot.Values.Sum();
        var percentual = bytesTotais <= 0 ? 0 : Math.Min(100, (double)enviados / bytesTotais * 100);
        aoAtualizar(percentual);
    }
}
