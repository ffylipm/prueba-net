using Prueba.Core.DTOs;
using Prueba.Core.Enums;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Tests.Helpers;

/// <summary>Simula que la base de datos falla justo al guardar Completed (después de enviar al proveedor).</summary>
public class FailOnCompletedRequestService(IRequestService inner) : IRequestService
{
    public async Task<Request> AddRequestAsync(RequestDTO dto, CancellationToken ct = default)
    {
        return await inner.AddRequestAsync(dto, ct);
    }

    public async Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default)
    {
        return await inner.GetRequestByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default)
    {
        return await inner.GetRequestsAsync(status, ct);
    }

    public async Task UpdateRequestStatusAsync(Request request, RequestStatus to, CancellationToken ct = default)
    {
        if (to == RequestStatus.Completed)
        {
            throw new InvalidOperationException("database is locked (simulado)");
        }

        await inner.UpdateRequestStatusAsync(request, to, ct);
    }
}
