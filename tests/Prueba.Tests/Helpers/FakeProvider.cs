using Prueba.Core.Exceptions;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Tests.Helpers;

/// <summary>Proveedor con respuestas programadas: true = éxito, false = fallo transitorio.</summary>
public class FakeProvider(params bool[] script) : IExternalProvider
{
    private readonly Queue<bool> _script = new(script);

    public List<string> Calls { get; } = [];

    public Task SendAsync(Request request, CancellationToken ct = default)
    {
        Calls.Add(request.ExternalReference);
        var ok = _script.Count == 0 || _script.Dequeue();

        if (!ok)
        {
            throw new ProviderException("fallo simulado");
        }

        return Task.CompletedTask;
    }
}
