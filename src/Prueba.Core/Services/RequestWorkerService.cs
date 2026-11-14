using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prueba.Core.Enums;
using Prueba.Core.Exceptions;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Services;

/// <summary>
/// No hay transacción posible entre el proveedor externo y la base de datos, así que la garantía es
/// "al menos una vez" + proveedor idempotente por ExternalReference:
/// 1. Tomar la solicitud (Pending → Processing, Attempts + 1) es un solo UPDATE atómico.
/// 2. Un fallo del proveedor devuelve la solicitud a Pending o la marca Failed.
/// 3. Un fallo al guardar DESPUÉS de un envío exitoso no la devuelve a Pending: queda en Processing
///    y solo se recupera tras ProcessingTimeoutSeconds, cuando reenviar es seguro por idempotencia.
/// </summary>
public class RequestWorkerService(
    IRequestRepository repository,
    IRequestService requestService,
    IExternalProvider provider,
    IOptions<WorkerOptions> options,
    ILogger<RequestWorkerService> logger) : IRequestWorkerService
{
    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var o = options.Value;

        var recovered = await repository.ResetStuckProcessingRequestsAsync(
            TimeSpan.FromSeconds(o.ProcessingTimeoutSeconds), ct);
        if (recovered > 0)
        {
            logger.LogWarning("{Count} solicitudes atascadas en Processing volvieron a Pending", recovered);
        }

        var pending = await repository.GetPendingRequestsAsync(o.BatchSize, ct);
        var processed = 0;

        foreach (var candidate in pending)
        {
            ct.ThrowIfCancellationRequested();

            // UPDATE condicional: si otro Worker ya la tomó, se salta. Evita doble envío.
            if (!await repository.TryUpdateRequestToProcessingAsync(candidate.Id, ct))
            {
                continue;
            }

            var request = await repository.GetRequestByIdAsync(candidate.Id, ct);
            if (request is null)
            {
                continue;
            }

            processed++;

            if (!await TrySendAsync(request, o.MaxAttempts, ct))
            {
                continue;
            }

            await MarkCompletedAsync(request, ct);
        }

        return processed;
    }

    /// <summary>Llama al proveedor. Si falla, deja la solicitud en Pending o Failed y devuelve false.</summary>
    private async Task<bool> TrySendAsync(Request request, int maxAttempts, CancellationToken ct)
    {
        try
        {
            await provider.SendAsync(request, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var next = request.Attempts >= maxAttempts ? RequestStatus.Failed : RequestStatus.Pending;

            if (ex is ProviderException)
            {
                logger.LogWarning("{ExternalReference} falló (intento {Attempt}/{Max}) → {Status}",
                    request.ExternalReference, request.Attempts, maxAttempts, next);
            }
            else
            {
                logger.LogError(ex, "{ExternalReference} error inesperado (intento {Attempt}/{Max}) → {Status}",
                    request.ExternalReference, request.Attempts, maxAttempts, next);
            }

            await requestService.UpdateRequestStatusAsync(request, next, ct);
            return false;
        }
    }

    /// <summary>
    /// El proveedor ya aplicó el efecto. Si guardar falla, NO se vuelve a Pending:
    /// la solicitud queda en Processing hasta que la recuperación por timeout la reintente.
    /// </summary>
    private async Task MarkCompletedAsync(Request request, CancellationToken ct)
    {
        try
        {
            await requestService.UpdateRequestStatusAsync(request, RequestStatus.Completed, ct);
            logger.LogInformation("{ExternalReference} → Completed (intento {Attempt})",
                request.ExternalReference, request.Attempts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "{ExternalReference} enviada al proveedor pero no se pudo guardar Completed; queda en Processing",
                request.ExternalReference);
        }
    }
}
