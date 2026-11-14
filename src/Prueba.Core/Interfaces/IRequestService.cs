using Prueba.Core.DTOs;
using Prueba.Core.Enums;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Interfaces;

public interface IRequestService
{
    Task<Request> AddRequestAsync(RequestDTO dto, CancellationToken ct = default);
    Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default);
    Task UpdateRequestStatusAsync(Request request, RequestStatus to, CancellationToken ct = default);
}
