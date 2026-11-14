using Prueba.Core.Enums;

namespace Prueba.Core.Persistence.Entities;

public class Request
{
    public int Id { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Solo RequestService.UpdateRequestStatusAsync debe cambiarlo (regla 5).
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public int Attempts { get; set; }
    public DateTimeOffset InsertAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
