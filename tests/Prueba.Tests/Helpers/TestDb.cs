using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Prueba.Core.Persistence.DBContexts;

namespace Prueba.Tests.Helpers;

/// <summary>
/// SQLite en memoria con el esquema real (migraciones). Se usa SQLite y no el proveedor InMemory
/// porque las pruebas dependen del índice único y del UPDATE condicional.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public RequestDBContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RequestDBContext>().UseSqlite(_connection).Options;
        return new RequestDBContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
