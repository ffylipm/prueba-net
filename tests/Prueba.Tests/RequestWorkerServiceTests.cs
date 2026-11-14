using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Prueba.Core.DTOs;
using Prueba.Core.Enums;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence;
using Prueba.Core.Persistence.Entities;
using Prueba.Core.Services;
using Prueba.Tests.Helpers;

namespace Prueba.Tests;

/// <summary>
/// Riesgo principal: efectos duplicados en el proveedor y estados incorrectos ante fallos transitorios
/// (reglas 3, 4 y 5). Usa SQLite real en memoria.
/// </summary>
public class RequestWorkerServiceTests : IDisposable
{
    private readonly TestDb _testDb = new();
    private readonly FakeTimeProvider _clock = new();

    public void Dispose()
    {
        _testDb.Dispose();
    }

    private async Task<int> SeedAsync(string externalReference = "OP-1")
    {
        await using var db = _testDb.CreateContext();
        var service = new RequestService(new RequestRepository(db, _clock), _clock);
        var r = await service.AddRequestAsync(new RequestDTO
        {
            ExternalReference = externalReference, CustomerId = "C1", ServiceCode = "S1", Description = "D",
        });
        return r.Id;
    }

    private async Task<int> RunCycleAsync(FakeProvider provider, int maxAttempts = 3, bool failOnCompleted = false)
    {
        await using var db = _testDb.CreateContext();
        var repo = new RequestRepository(db, _clock);
        IRequestService service = new RequestService(repo, _clock);
        if (failOnCompleted)
        {
            service = new FailOnCompletedRequestService(service);
        }

        var worker = new RequestWorkerService(
            repo, service, provider,
            Options.Create(new WorkerOptions
            {
                MaxAttempts = maxAttempts,
                BatchSize = 10,
                ProcessingTimeoutSeconds = ProcessingTimeoutSeconds,
            }),
            NullLogger<RequestWorkerService>.Instance);
        return await worker.ProcessPendingAsync();
    }

    private const int ProcessingTimeoutSeconds = 60;

    private async Task<Request> LoadAsync(int id)
    {
        await using var db = _testDb.CreateContext();
        return db.Requests.Single(r => r.Id == id);
    }

    [Fact]
    public async Task Exito_marca_Completed_con_un_solo_envio()
    {
        var id = await SeedAsync();
        var provider = new FakeProvider(true);

        await RunCycleAsync(provider);

        var r = await LoadAsync(id);
        Assert.Equal(RequestStatus.Completed, r.Status);
        Assert.Equal(1, r.Attempts);
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Fallo_transitorio_vuelve_a_Pending_y_luego_completa()
    {
        var id = await SeedAsync();
        var provider = new FakeProvider(false, true);

        await RunCycleAsync(provider);
        var afterFirst = await LoadAsync(id);
        Assert.Equal(RequestStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);

        await RunCycleAsync(provider);
        var afterSecond = await LoadAsync(id);
        Assert.Equal(RequestStatus.Completed, afterSecond.Status);
        Assert.Equal(2, afterSecond.Attempts);
    }

    [Fact]
    public async Task Fallo_persistente_marca_Failed_al_llegar_a_MaxAttempts()
    {
        var id = await SeedAsync();
        var provider = new FakeProvider(false, false, false);

        for (var i = 0; i < 3; i++)
        {
            await RunCycleAsync(provider);
        }

        var r = await LoadAsync(id);
        Assert.Equal(RequestStatus.Failed, r.Status);
        Assert.Equal(3, r.Attempts);

        // Failed es final: otro ciclo no vuelve a llamar al proveedor.
        await RunCycleAsync(provider);
        Assert.Equal(3, provider.Calls.Count);
    }

    [Fact]
    public async Task Completed_no_se_vuelve_a_enviar_en_ciclos_siguientes()
    {
        await SeedAsync();
        var provider = new FakeProvider(true);

        await RunCycleAsync(provider);
        await RunCycleAsync(provider);
        await RunCycleAsync(provider);

        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task TryUpdateRequestToProcessing_solo_un_proceso_puede_tomar_la_solicitud()
    {
        var id = await SeedAsync();

        await using var db1 = _testDb.CreateContext();
        await using var db2 = _testDb.CreateContext();

        var first = await new RequestRepository(db1, _clock).TryUpdateRequestToProcessingAsync(id);
        var second = await new RequestRepository(db2, _clock).TryUpdateRequestToProcessingAsync(id);

        Assert.True(first);
        Assert.False(second);

        // El intento se cuenta en el mismo UPDATE que la toma: sobrevive a una caÃ­da del proceso.
        var r = await LoadAsync(id);
        Assert.Equal(RequestStatus.Processing, r.Status);
        Assert.Equal(1, r.Attempts);
    }

    [Fact]
    public async Task Fallo_al_guardar_Completed_tras_enviar_no_reenvia_inmediatamente()
    {
        var id = await SeedAsync();
        var provider = new FakeProvider(true, true);

        await RunCycleAsync(provider, failOnCompleted: true);

        // El proveedor ya aplicÃ³ el efecto; la solicitud NO vuelve a Pending.
        var r = await LoadAsync(id);
        Assert.Equal(RequestStatus.Processing, r.Status);
        Assert.Single(provider.Calls);

        // Ciclos siguientes antes del timeout no la reenvÃ­an.
        _clock.Advance(TimeSpan.FromSeconds(ProcessingTimeoutSeconds - 1));
        await RunCycleAsync(provider);
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Processing_atascada_se_recupera_tras_timeout_y_completa()
    {
        var id = await SeedAsync();
        var provider = new FakeProvider(true, true);

        await RunCycleAsync(provider, failOnCompleted: true);
        Assert.Equal(RequestStatus.Processing, (await LoadAsync(id)).Status);

        // Pasado el timeout se reintenta; es seguro porque el proveedor es idempotente por ExternalReference.
        _clock.Advance(TimeSpan.FromSeconds(ProcessingTimeoutSeconds + 1));
        await RunCycleAsync(provider);

        var r = await LoadAsync(id);
        Assert.Equal(RequestStatus.Completed, r.Status);
        Assert.Equal(2, r.Attempts);
        Assert.Equal(["OP-1", "OP-1"], provider.Calls);
    }
}
