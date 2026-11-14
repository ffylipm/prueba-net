using Prueba.Core.DTOs;
using Prueba.Core.Enums;
using Prueba.Core.Exceptions;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Services;

public class RequestService(IRequestRepository repository, TimeProvider clock) : IRequestService
{
    // Transiciones permitidas. Completed y Failed son finales (regla 5, supuesto A3).
    private static readonly Dictionary<RequestStatus, RequestStatus[]> AllowedTransitions = new()
    {
        [RequestStatus.Pending] = [RequestStatus.Processing],
        [RequestStatus.Processing] = [RequestStatus.Completed, RequestStatus.Pending, RequestStatus.Failed],
        [RequestStatus.Completed] = [],
        [RequestStatus.Failed] = [],
    };

    public static bool CanTransition(RequestStatus from, RequestStatus to)
    {
        return AllowedTransitions[from].Contains(to);
    }

    public async Task<Request> AddRequestAsync(RequestDTO dto, CancellationToken ct = default)
    {
        var externalReference = dto.ExternalReference.Trim();

        if (await repository.GetRequestByExternalReferenceAsync(externalReference, ct) is not null)
        {
            throw new DuplicateExternalReferenceException(externalReference);
        }

        var now = clock.GetUtcNow();
        var request = new Request
        {
            ExternalReference = externalReference,
            CustomerId = dto.CustomerId.Trim(),
            ServiceCode = dto.ServiceCode.Trim(),
            Description = dto.Description.Trim(),
            Status = RequestStatus.Pending,
            Attempts = 0,
            InsertAt = now,
            UpdatedAt = now,
        };

        await repository.AddRequestAsync(request, ct);

        return request;
    }

    public async Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default)
    {
        return await repository.GetRequestByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default)
    {
        return await repository.GetRequestsAsync(status, ct);
    }

    public async Task UpdateRequestStatusAsync(Request request, RequestStatus to, CancellationToken ct = default)
    {
        if (!CanTransition(request.Status, to))
        {
            throw new InvalidStatusTransitionException(request.Status, to);
        }

        request.Status = to;
        request.UpdatedAt = clock.GetUtcNow();
        await repository.UpdateRequestAsync(request, ct);
    }
}
