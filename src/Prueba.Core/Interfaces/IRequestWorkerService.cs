namespace Prueba.Core.Interfaces;

public interface IRequestWorkerService
{
    /// <summary>Procesa un lote de solicitudes Pending. Devuelve cuántas tomó.</summary>
    Task<int> ProcessPendingAsync(CancellationToken ct = default);
}
