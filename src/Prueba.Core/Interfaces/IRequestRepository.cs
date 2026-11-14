using Prueba.Core.Enums;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Interfaces;

public interface IRequestRepository
{
    Task AddRequestAsync(Request request, CancellationToken ct = default);
    Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default);
    Task<Request?> GetRequestByExternalReferenceAsync(string externalReference, CancellationToken ct = default);
    Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default);
    Task UpdateRequestAsync(Request request, CancellationToken ct = default);
}
