# Portal de solicitudes y procesamiento en segundo plano

Prueba técnica Full Stack .NET.

## Cómo ejecutar

Requisitos: .NET SDK 10.

1. API en `http://localhost:5112`. Crea `data/portal.db` al iniciar.

   ```
   dotnet run --project src/Prueba.Api
   ```

2. Worker, en otra terminal:

   ```
   dotnet run --project src/Prueba.Worker
   ```

3. Pruebas:

   ```
   dotnet test
   ```

Para probar el flujo, crear una solicitud y consultarla. Pasa de `Pending` a `Completed`, o a `Failed` tras 3 fallos del proveedor simulado:

```
curl -X POST http://localhost:5112/api/requests -H "Content-Type: application/json" -d "{\"externalReference\":\"OP-1\",\"customerId\":\"C1\",\"serviceCode\":\"S1\",\"description\":\"Prueba\"}"
curl http://localhost:5112/api/requests
```

Más ejemplos en `src/Prueba.Api/Prueba.Api.http`. Para reiniciar la base, borrar `data/portal.db`.

### Worker como Windows Service

En PowerShell como administrador, desde la raíz del repositorio:

```
powershell -ExecutionPolicy Bypass -File scripts\install-worker-service.ps1
```

Publica el Worker en `C:\Services\PruebaWorker`, configura la ruta absoluta de `data\portal.db` (la misma que usa la API), crea el servicio `PruebaWorker` con inicio automático y reinicio ante fallos, y lo arranca. Si ya existe, lo reemplaza. Parámetros opcionales: `-ServiceName`, `-InstallPath`, `-DatabasePath`.

Para quitarlo:

```
powershell -ExecutionPolicy Bypass -File scripts\uninstall-worker-service.ps1
```

## El problema

Una empresa registra solicitudes de servicio desde un portal web y las envía a un proveedor externo. Hay tres dificultades:

- Los usuarios reintentan cuando el sistema va lento, así que la misma solicitud puede llegar dos veces.
- El proveedor a veces falla de forma temporal.
- Una solicitud (mismo `externalReference`) nunca debe procesarse dos veces en el proveedor.

Solución:

- Una API para registrar solicitudes y consultarlas por estado.
- Una pantalla para crearlas, verlas y filtrarlas.
- Una base de datos real.
- Un proceso en segundo plano, instalable como Windows Service, que envíe las pendientes al proveedor externo y actualice su estado.

## 1. Schema

Tabla `Request`:

| Columna             | Tipo                    | Restricciones             | Origen                |
|---------------------|-------------------------|---------------------------|-----------------------|
| `Id`                | `int`                   | PK, autoincremental       | Agregado              |
| `ExternalReference` | `string` (100)          | Obligatorio, índice único | PDF, reglas 1 y 3     |
| `CustomerId`        | `string` (50)           | Obligatorio               | PDF, regla 1          |
| `ServiceCode`       | `string` (50)           | Obligatorio               | PDF, regla 1          |
| `Description`       | `string` (500)          | Obligatorio               | PDF, regla 1          |
| `Status`            | `RequestStatus` (texto) | Obligatorio, índice       | PDF, reglas 1, 2 y 5  |
| `Attempts`          | `int`                   | Inicia en 0               | Agregado (reintentos) |
| `InsertAt`          | `DateTimeOffset`        | Obligatorio               | Agregado              |
| `UpdatedAt`         | `DateTimeOffset`        | Obligatorio               | Agregado              |

## 2. Estructura de la solución

Solución .NET 10 (`Prueba.sln`), base de datos SQLite con EF Core.

```
src/
├── Prueba.Api                     API ASP.NET Core
│   ├── Controllers
│   │   └── RequestController.cs                     POST/GET /api/requests
│   ├── Middlewares
│   │   └── ExceptionHandlingMiddleware.cs           Excepciones → ProblemDetails (409 / 500)
│   ├── Program.cs                                   Registro de dependencias, CORS, migraciones al iniciar
│   └── Prueba.Api.http                              Peticiones de ejemplo
├── Prueba.Worker                  Proceso en segundo plano (Windows Service)
│   ├── ProcessingWorker.cs                          BackgroundService: un ciclo cada PollIntervalSeconds
│   └── Program.cs                                   AddWindowsService, registro de dependencias, migraciones
└── Prueba.Core                    Dominio, persistencia y contratos
    ├── DTOs
    │   └── RequestDTO.cs          RequestDTO (entrada) y RequestResponseDTO (salida)
    ├── Enums
    │   └── RequestStatus.cs       Pending, Processing, Completed, Failed
    ├── Exceptions
    │   ├── DuplicateExternalReferenceException.cs   409: mismo externalReference (regla 3)
    │   ├── InvalidStatusTransitionException.cs      409: transición no permitida (regla 5)
    │   └── ProviderException.cs                     Fallo transitorio del proveedor (regla 4)
    ├── Interfaces
    │   ├── IExternalProvider.cs
    │   ├── IRequestRepository.cs
    │   ├── IRequestService.cs
    │   └── IRequestWorkerService.cs
    ├── Persistence
    │   ├── Entities
    │   │   └── Request.cs
    │   ├── DBContexts
    │   │   └── RequestDBContext.cs                  Índice único en ExternalReference, índice en Status
    │   ├── Migrations
    │   │   └── InitialCreate                        Crea la tabla Request
    │   └── RequestRepository.cs                     Traduce la violación del índice único a DuplicateExternalReferenceException
    └── Services
        ├── ExternalProviderSimulation.cs            Proveedor simulado: fallo transitorio aleatorio, idempotente por ExternalReference
        ├── RequestService.cs                        Alta con control de duplicados y transiciones de estado válidas
        ├── RequestWorkerService.cs                  Toma Pending, llama al proveedor y actualiza el estado
        └── WorkerOptions.cs                         WorkerOptions y ProviderOptions (appsettings del Worker)

scripts/
├── install-worker-service.ps1                       Publica e instala el Worker como Windows Service
└── uninstall-worker-service.ps1

tests/
└── Prueba.Tests                   xUnit
    ├── Helpers                                      TestDb (SQLite en memoria), FakeProvider, ApiFactory, FailOnCompletedRequestService
    ├── RequestControllerTests.cs
    ├── RequestRepositoryTests.cs
    ├── RequestWorkerServiceTests.cs
    └── StatusRulesTests.cs
```

## 3. API

Base: `http://localhost:5112`

| Método | Ruta                          | Respuesta                                                   |
|--------|-------------------------------|-------------------------------------------------------------|
| POST   | `/api/requests`               | `201` con la solicitud en `Pending` y header `Location`     |
| GET    | `/api/requests?status=Pending`| `200` con la lista, filtro opcional por estado              |
| GET    | `/api/requests/{id}`          | `200` con la solicitud, `404` si no existe                  |

Cuerpo del POST:

```json
{
  "externalReference": "OP-1001",
  "customerId": "CUST-01",
  "serviceCode": "INSTALL",
  "description": "Instalación de equipo"
}
```

Errores (formato ProblemDetails, RFC 7807):

| Código | Cuándo                                                                 |
|--------|------------------------------------------------------------------------|
| `400`  | Campos vacíos o demasiado largos; `status` con un valor que no existe  |
| `409`  | Ya existe una solicitud con el mismo `externalReference` (regla 3)     |
| `500`  | Error no controlado; el detalle va al log, no a la respuesta           |

Duplicados: el servicio busca el `externalReference` antes de insertar, y el índice único cubre el caso de dos POST simultáneos.

Base de datos: la API y el Worker aplican las migraciones de EF Core al iniciar, así que `data/portal.db` se crea con el schema sin pasos manuales.

## 4. Worker

Cada `PollIntervalSeconds` el Worker:

1. Devuelve a `Pending` las solicitudes que llevan en `Processing` más de `ProcessingTimeoutSeconds` (proceso caído o fallo al guardar).
2. Lee un lote de `Pending` (`BatchSize`).
3. Por cada una, la toma con un `UPDATE` condicional: `Pending → Processing` y `Attempts + 1` en una sola sentencia. Si otro proceso ya la tomó, la salta.
4. Llama al proveedor simulado con `ExternalReference` como clave de idempotencia.
   - Éxito → `Completed`.
   - Fallo transitorio → vuelve a `Pending`; al llegar a `MaxAttempts` pasa a `Failed`.
   - Éxito pero falla el guardado → queda en `Processing` y no se reenvía hasta el timeout del paso 1.

Estados: `Pending → Processing → Completed | Pending | Failed`. `Completed` y `Failed` son finales (regla 5).

No hay transacción posible entre el proveedor y la base de datos: la garantía es "al menos una vez" con un proveedor idempotente por `ExternalReference` (regla 3).

Configuración en `src/Prueba.Worker/appsettings.json`:

| Clave                             | Por defecto | Uso                                               |
|-----------------------------------|-------------|---------------------------------------------------|
| `Worker:PollIntervalSeconds`      | 5           | Segundos entre ciclos                             |
| `Worker:BatchSize`                | 10          | Solicitudes por ciclo                             |
| `Worker:MaxAttempts`              | 3           | Intentos antes de `Failed`                        |
| `Worker:ProcessingTimeoutSeconds` | 60          | Tiempo para considerar atascada una solicitud     |
| `Provider:FailureRate`            | 0.3         | Probabilidad de fallo transitorio del simulado    |
| `Provider:LatencyMs`              | 300         | Latencia simulada                                 |

El Worker usa `AddWindowsService`: en consola corre normal y, instalado como servicio, se integra con el Service Control Manager y el Event Log. Instalación en [Worker como Windows Service](#worker-como-windows-service).

## 5. Pruebas

27 pruebas (`dotnet test`) sobre los riesgos principales: duplicados, estados y fallos del proveedor. Usan SQLite en memoria con el esquema real, porque dependen del índice único y del `UPDATE` condicional.

| Archivo                         | Escenarios                                                                                          |
|---------------------------------|-----------------------------------------------------------------------------------------------------|
| `StatusRulesTests`              | `Completed` y `Failed` son finales; transiciones válidas; salir de `Completed` lanza error y no persiste |
| `RequestRepositoryTests`        | La violación del índice único es duplicado; otro error de base de datos no se reporta como duplicado |
| `RequestWorkerServiceTests`     | Éxito con un solo envío; fallo transitorio y luego éxito; `Failed` tras `MaxAttempts`; `Completed` no se reenvía; solo un proceso toma la solicitud; fallo al guardar tras enviar no reenvía; recuperación tras timeout |
| `RequestControllerTests`        | POST crea `Pending`; POST repetido → 409; 10 POST simultáneos crean una sola solicitud; campos vacíos → 400; filtro por estado y estado inválido → 400 |
