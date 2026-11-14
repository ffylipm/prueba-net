using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Prueba.Core.Interfaces;
using Prueba.Core.Persistence;
using Prueba.Core.Persistence.DBContexts;
using Prueba.Core.Services;
using Prueba.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Como Windows Service: integra con el SCM y usa el Event Log. En consola no hace nada.
builder.Services.AddWindowsService(o => o.ServiceName = "PruebaWorker");

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
builder.Services.AddScoped<IRequestWorkerService, RequestWorkerService>();
builder.Services.AddSingleton<IExternalProvider, ExternalProviderSimulation>();

// Configuración
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.Section));
builder.Services.Configure<ProviderOptions>(builder.Configuration.GetSection(ProviderOptions.Section));

builder.Services.AddHostedService<ProcessingWorker>();

var host = builder.Build();

// Crea o actualiza la base de datos al iniciar.
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RequestDBContext>().Database.Migrate();
}

host.Run();
