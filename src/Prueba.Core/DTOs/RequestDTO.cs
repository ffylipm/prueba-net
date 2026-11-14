using System.ComponentModel.DataAnnotations;
using Prueba.Core.Enums;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.DTOs;

/// <summary>Entrada para registrar una solicitud.</summary>
public record RequestDTO
{
    [Required(AllowEmptyStrings = false), StringLength(100)]
    public string ExternalReference { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(50)]
    public string CustomerId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(50)]
    public string ServiceCode { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(500)]
    public string Description { get; init; } = string.Empty;
}

/// <summary>Salida de la API: no expone la entidad directamente.</summary>
public record RequestResponseDTO(
    int Id,
    string ExternalReference,
    string CustomerId,
    string ServiceCode,
    string Description,
    RequestStatus Status,
    int Attempts,
    DateTimeOffset InsertAt,
    DateTimeOffset UpdatedAt)
{
    public static RequestResponseDTO From(Request r)
    {
        return new RequestResponseDTO(
            r.Id, r.ExternalReference, r.CustomerId, r.ServiceCode, r.Description,
            r.Status, r.Attempts, r.InsertAt, r.UpdatedAt);
    }
}
