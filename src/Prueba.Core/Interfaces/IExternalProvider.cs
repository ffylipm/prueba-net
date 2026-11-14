using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Interfaces;

public interface IExternalProvider
{
    /// <summary>
    /// Envía la solicitud al proveedor usando ExternalReference como clave de idempotencia.
    /// Lanza <see cref="Exceptions.ProviderException"/> ante un fallo transitorio.
    /// </summary>
    Task SendAsync(Request request, CancellationToken ct = default);
}
