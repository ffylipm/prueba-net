namespace Prueba.Core.Exceptions;

/// <summary>Fallo transitorio del proveedor externo (regla 4). Se puede reintentar.</summary>
public class ProviderException(string message) : Exception(message);
