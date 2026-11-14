using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Prueba.Core.Enums;
using Prueba.Core.Exceptions;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence.DBContexts;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Persistence;

public class RequestRepository(RequestDBContext db) : IRequestRepository
{
    // SQLITE_CONSTRAINT_UNIQUE: https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;

    public async Task AddRequestAsync(Request request, CancellationToken ct = default)
    {
        db.Requests.Add(request);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateExternalReference(ex))
        {
            // Dos POST simultáneos pasaron la búsqueda previa: el índice único decide.
            db.Entry(request).State = EntityState.Detached;
            throw new DuplicateExternalReferenceException(request.ExternalReference, ex);
        }
    }

    /// <summary>
    /// Solo una violación del índice único de ExternalReference es un duplicado.
    /// Cualquier otro error (NOT NULL, base bloqueada, disco lleno...) se propaga tal cual.
    /// </summary>
    private static bool IsDuplicateExternalReference(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException sqlite
            && sqlite.SqliteExtendedErrorCode == SqliteConstraintUnique
            && sqlite.Message.Contains($"{nameof(Request)}.{nameof(Request.ExternalReference)}", StringComparison.Ordinal);
    }

    public async Task<Request?> GetRequestByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Requests.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<Request?> GetRequestByExternalReferenceAsync(string externalReference, CancellationToken ct = default)
    {
        return await db.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExternalReference == externalReference, ct);
    }

    public async Task<IReadOnlyList<Request>> GetRequestsAsync(RequestStatus? status, CancellationToken ct = default)
    {
        var query = db.Requests.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        return await query.OrderBy(r => r.InsertAt).ToListAsync(ct);
    }

    public async Task UpdateRequestAsync(Request request, CancellationToken ct = default)
    {
        if (db.Entry(request).State == EntityState.Detached)
        {
            db.Requests.Update(request);
        }

        await db.SaveChangesAsync(ct);
    }
}
