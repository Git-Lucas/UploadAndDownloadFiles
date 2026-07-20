using UploadAndDownloadFiles.Aplicacao.CasosDeUso;

namespace UploadAndDownloadFiles.Infraestrutura.Jobs;

/// <summary>
/// Executa a <see cref="ReconciliarArquivos"/> uma vez por dia. Cria um escopo de DI a cada
/// execução, já que o repositório e o `DbContext` são registrados como `Scoped`.
/// </summary>
public sealed class ReconciliacaoDiariaBackgroundService(IServiceScopeFactory fabricaDeEscopos, ILogger<ReconciliacaoDiariaBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloEntreExecucoes = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _fabricaDeEscopos = fabricaDeEscopos;
    private readonly ILogger<ReconciliacaoDiariaBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = _fabricaDeEscopos.CreateScope();
                var reconciliarArquivos = escopo.ServiceProvider.GetRequiredService<ReconciliarArquivos>();
                await reconciliarArquivos.ExecutarAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao executar a reconciliação diária de arquivos.");
            }

            await Task.Delay(IntervaloEntreExecucoes, stoppingToken);
        }
    }
}
