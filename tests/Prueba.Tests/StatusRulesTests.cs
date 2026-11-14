using Microsoft.Extensions.Time.Testing;
using Prueba.Core.Enums;
using Prueba.Core.Exceptions;
using Prueba.Core.Persistence;
using Prueba.Core.Persistence.Entities;
using Prueba.Core.Services;
using Prueba.Tests.Helpers;

namespace Prueba.Tests;

/// <summary>Regla 5: una solicitud Completed no regresa a un estado anterior.</summary>
public class StatusRulesTests
{
    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Processing)]
    [InlineData(RequestStatus.Failed)]
    [InlineData(RequestStatus.Completed)]
    public void Completed_no_puede_cambiar_a_ningun_estado(RequestStatus to)
    {
        Assert.False(RequestService.CanTransition(RequestStatus.Completed, to));
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Processing)]
    [InlineData(RequestStatus.Completed)]
    public void Failed_es_final(RequestStatus to)
    {
        Assert.False(RequestService.CanTransition(RequestStatus.Failed, to));
    }

    [Theory]
    [InlineData(RequestStatus.Pending, RequestStatus.Processing)]
    [InlineData(RequestStatus.Processing, RequestStatus.Completed)]
    [InlineData(RequestStatus.Processing, RequestStatus.Pending)]
    [InlineData(RequestStatus.Processing, RequestStatus.Failed)]
    public void Transiciones_validas(RequestStatus from, RequestStatus to)
    {
        Assert.True(RequestService.CanTransition(from, to));
    }

    [Fact]
    public void Pending_no_salta_directo_a_Completed()
    {
        Assert.False(RequestService.CanTransition(RequestStatus.Pending, RequestStatus.Completed));
    }

    [Fact]
    public async Task UpdateRequestStatus_rechaza_salir_de_Completed_y_no_persiste()
    {
        using var testDb = new TestDb();
        var clock = new FakeTimeProvider();
        int id;

        await using (var db = testDb.CreateContext())
        {
            var seeded = new Request
            {
                ExternalReference = "OP-1", CustomerId = "C", ServiceCode = "S", Description = "D",
                Status = RequestStatus.Completed, InsertAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
            };
            db.Requests.Add(seeded);
            await db.SaveChangesAsync();
            id = seeded.Id;
        }

        await using (var db = testDb.CreateContext())
        {
            var service = new RequestService(new RequestRepository(db, clock), clock);
            var request = (await service.GetRequestByIdAsync(id))!;

            await Assert.ThrowsAsync<InvalidStatusTransitionException>(
                () => service.UpdateRequestStatusAsync(request, RequestStatus.Pending));
        }

        await using (var db = testDb.CreateContext())
        {
            Assert.Equal(RequestStatus.Completed, db.Requests.Single(r => r.Id == id).Status);
        }
    }
}
