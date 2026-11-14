using Microsoft.Extensions.Options;
using Prueba.Core.Interfaces;
using Prueba.Core.Services;

namespace Prueba.Worker;

/// <summary>
/// Ciclo del proceso en segundo plano: cada PollIntervalSeconds toma un lote de Pending.
/// Usa un scope por ciclo para que el DbContext no viva toda la vida del servicio.
/// </summary>
public class ProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        logger.LogInformation("Worker iniciado. Intervalo: {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRequestWorkerService>();
                var count = await service.ProcessPendingAsync(stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("Ciclo terminado: {Count} solicitudes procesadas", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un ciclo fallido no debe tumbar el servicio; se reintenta en el siguiente.
                logger.LogError(ex, "Error en el ciclo de procesamiento");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
