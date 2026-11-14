namespace Prueba.Core.Exceptions;

/// <summary>Ya existe una solicitud con el mismo externalReference (regla 3). La API la traduce a 409.</summary>
public class DuplicateExternalReferenceException(string externalReference, Exception? inner = null)
    : Exception($"Ya existe una solicitud con externalReference '{externalReference}'.", inner)
{
    public string ExternalReference { get; } = externalReference;
}
