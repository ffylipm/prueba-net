using Prueba.Core.Enums;

namespace Prueba.Core.Exceptions;

/// <summary>Transición de estado no permitida (p. ej. Completed → cualquier otro, regla 5).</summary>
public class InvalidStatusTransitionException(RequestStatus from, RequestStatus to)
    : Exception($"No se permite cambiar el estado de {from} a {to}.")
{
    public RequestStatus From { get; } = from;
    public RequestStatus To { get; } = to;
}
