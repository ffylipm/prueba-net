using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Prueba.Core.Persistence.Entities;

namespace Prueba.Core.Persistence.DBContexts;

public class RequestDBContext(DbContextOptions<RequestDBContext> options) : DbContext(options)
{
    public DbSet<Request> Requests => Set<Request>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Request>();
        e.ToTable("Request");
        e.HasKey(x => x.Id);

        e.Property(x => x.ExternalReference).IsRequired().HasMaxLength(100);
        e.HasIndex(x => x.ExternalReference).IsUnique();

        e.Property(x => x.CustomerId).IsRequired().HasMaxLength(50);
        e.Property(x => x.ServiceCode).IsRequired().HasMaxLength(50);
        e.Property(x => x.Description).IsRequired().HasMaxLength(500);

        e.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => x.Status);

        e.Property(x => x.Attempts).HasDefaultValue(0);

        // SQLite no ordena ni compara DateTimeOffset de forma nativa: se guarda como long.
        var dto = new DateTimeOffsetToBinaryConverter();
        e.Property(x => x.InsertAt).IsRequired().HasConversion(dto);
        e.Property(x => x.UpdatedAt).IsRequired().HasConversion(dto);
    }
}
