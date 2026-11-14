using Prueba.Core.Enums;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Interfaces;

public interface IRequestRepository
{
    Task AddRequestAsync(Request request, CancellationToken ct = default);
    Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default);
    Task<Request?> GetRequestByExternalReferenceAsync(string externalReference, CancellationToken ct = default);
    Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<Request>> GetPendingRequestsAsync(int batchSize, CancellationToken ct = default);
    Task UpdateRequestAsync(Request request, CancellationToken ct = default);

    /// <summary>
    /// Pasa una solicitud de Pending a Processing e incrementa Attempts en un solo UPDATE condicional.
    /// Devuelve false si otro proceso ya la tomó.
    /// </summary>
    Task<bool> TryUpdateRequestToProcessingAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Devuelve a Pending las solicitudes que llevan en Processing más de <paramref name="timeout"/>
    /// (proceso caído o fallo al guardar tras enviar). Devuelve cuántas se recuperaron.
    /// </summary>
    Task<int> ResetStuckProcessingRequestsAsync(TimeSpan timeout, CancellationToken ct = default);
}
