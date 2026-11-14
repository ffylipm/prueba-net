using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Prueba.Api.Middlewares;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence;
using Prueba.Core.Persistence.DBContexts;
using Prueba.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Persistencia
var connectionString = builder.Configuration.GetConnectionString("Main");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Falta ConnectionStrings:Main en la configuración.");
}

// SQLite no crea la carpeta del archivo.
var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dataSource))!);

builder.Services.AddDbContext<RequestDBContext>(o => o.UseSqlite(connectionString));

// Servicios
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRequestService, RequestService>();

builder.Services
    // Mantiene el sufijo Async en los nombres de acción para que nameof(...Async) funcione en CreatedAtAction.
    .AddControllers(o => o.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type")));

var app = builder.Build();

// Crea o actualiza la base de datos al iniciar.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RequestDBContext>().Database.Migrate();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();

app.Run();

// Expuesto para WebApplicationFactory en las pruebas.
public partial class Program;
