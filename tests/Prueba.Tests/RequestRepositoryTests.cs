using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Prueba.Core.Enums;
using Prueba.Core.Exceptions;
using Prueba.Core.Persistence;
using Prueba.Core.Persistence.Entities;
using Prueba.Tests.Helpers;

namespace Prueba.Tests;

/// <summary>Solo la violación del índice único de ExternalReference debe tratarse como duplicado.</summary>
public class RequestRepositoryTests : IDisposable
{
    private readonly TestDb _testDb = new();
    private readonly FakeTimeProvider _clock = new();

    public void Dispose()
    {
        _testDb.Dispose();
    }

    private Request NewRequest(string externalReference)
    {
        return new Request
        {
            ExternalReference = externalReference,
            CustomerId = "C1",
            ServiceCode = "S1",
            Description = "D",
            Status = RequestStatus.Pending,
            InsertAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow(),
        };
    }

    [Fact]
    public async Task Indice_unico_de_ExternalReference_lanza_DuplicateExternalReferenceException()
    {
        await using (var db = _testDb.CreateContext())
        {
            await new RequestRepository(db, _clock).AddRequestAsync(NewRequest("OP-1"));
        }

        // Inserción directa, sin la búsqueda previa del servicio: simula la carrera entre dos POST.
        await using (var db = _testDb.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<DuplicateExternalReferenceException>(
                () => new RequestRepository(db, _clock).AddRequestAsync(NewRequest("OP-1")));
            Assert.Equal("OP-1", ex.ExternalReference);
        }
    }

    [Fact]
    public async Task Otro_error_de_base_de_datos_no_se_reporta_como_duplicado()
    {
        await using var db = _testDb.CreateContext();
        var request = NewRequest("OP-2");
        request.CustomerId = null!; // viola NOT NULL, no el índice único

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => new RequestRepository(db, _clock).AddRequestAsync(request));

        Assert.IsType<DbUpdateException>(ex);
    }
}
