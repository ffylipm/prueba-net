namespace Prueba.Core.Services;

public class WorkerOptions
{
    public const string Section = "Worker";

    /// <summary>Segundos entre ciclos de procesamiento.</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>Máximo de solicitudes por ciclo.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Intentos antes de marcar Failed (supuesto A2).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Segundos tras los cuales una solicitud en Processing se considera atascada y vuelve a Pending.
    /// Debe ser mayor que el tiempo máximo de respuesta del proveedor.
    /// </summary>
    public int ProcessingTimeoutSeconds { get; set; } = 60;
}

public class ProviderOptions
{
    public const string Section = "Provider";

    /// <summary>Probabilidad (0..1) de que el proveedor simulado falle de forma transitoria.</summary>
    public double FailureRate { get; set; } = 0.3;

    /// <summary>Latencia simulada en milisegundos.</summary>
    public int LatencyMs { get; set; } = 300;
}
