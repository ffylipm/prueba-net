using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prueba.Core.Exceptions;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Services;

/// <summary>
/// Proveedor simulado (regla 4): responde éxito o fallo transitorio al azar.
/// Como un proveedor real con clave de idempotencia, si recibe dos veces el mismo
/// ExternalReference no produce un segundo efecto.
/// </summary>
public class ExternalProviderSimulation(IOptions<ProviderOptions> options, ILogger<ExternalProviderSimulation> logger)
    : IExternalProvider
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _effects = new();

    public int EffectCount
    {
        get { return _effects.Count; }
    }

    public async Task SendAsync(Request request, CancellationToken ct = default)
    {
        var o = options.Value;
        await Task.Delay(o.LatencyMs, ct);

        if (Random.Shared.NextDouble() < o.FailureRate)
        {
            logger.LogWarning("Proveedor: fallo transitorio para {ExternalReference}", request.ExternalReference);
            throw new ProviderException($"Fallo transitorio del proveedor para '{request.ExternalReference}'.");
        }

        if (!_effects.TryAdd(request.ExternalReference, DateTimeOffset.UtcNow))
        {
            logger.LogInformation("Proveedor: {ExternalReference} ya procesada, sin nuevo efecto", request.ExternalReference);
            return;
        }

        logger.LogInformation("Proveedor: {ExternalReference} procesada", request.ExternalReference);
    }
}
