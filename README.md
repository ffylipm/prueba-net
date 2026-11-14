# Portal de solicitudes y procesamiento en segundo plano

Prueba técnica Full Stack .NET.

## Cómo ejecutar

Requisitos: .NET SDK 10, Node.js 20+.

1. Backend (`http://localhost:5112`):

   ```
   dotnet run --project src/Prueba.Api
   ```

2. Servicio Windows, en modo consola:

   ```
   dotnet run --project src/Prueba.Worker
   ```

3. Frontend (`http://localhost:5173`):

   ```
   cd web
   npm install
   npm run dev
   ```

4. Pruebas:

   ```
   dotnet test
   ```

### Probar el flujo

1. Registrar una solicitud en `http://localhost:5173`. Queda en `Pending`.
2. Pulsar **Refrescar** tras unos segundos. Pasa a `Completed`, o a `Failed` si el proveedor simulado falla 3 veces.
3. Registrar otra con el mismo `externalReference`. Responde 409.

Endpoint `/api/requests`:

```
curl -X POST http://localhost:5112/api/requests -H "Content-Type: application/json" -d "{\"externalReference\":\"OP-1\",\"customerId\":\"C1\",\"serviceCode\":\"S1\",\"description\":\"Prueba\"}"
curl http://localhost:5112/api/requests
```

Más ejemplos: `src/Prueba.Api/Prueba.Api.http`.

### Reiniciar la base de datos

Con el Backend y el Servicio Windows detenidos, borrar `data/portal.db`. Se crea de nuevo al iniciar el Backend.

### Instalar el Servicio Windows

PowerShell como administrador, desde la raíz del repositorio:

```
powershell -ExecutionPolicy Bypass -File scripts\install-worker-service.ps1
```

El script:

- Publica `Prueba.Worker` en `C:\Services\PruebaWorker`.
- Configura la ruta absoluta de `data\portal.db`, la misma base del Backend.
- Crea el servicio `PruebaWorker` con inicio automático y reinicio ante fallos, y lo inicia.
- Reemplaza una instalación anterior.

Parámetros opcionales: `-ServiceName`, `-InstallPath`, `-DatabasePath`.

Desinstalar:

```
powershell -ExecutionPolicy Bypass -File scripts\uninstall-worker-service.ps1
```

## El problema

Una empresa registra solicitudes de servicio desde un portal web y las envía a un proveedor externo:

- Los usuarios reintentan cuando el sistema va lento, así que la misma solicitud puede llegar dos veces.
- El proveedor falla de forma transitoria.
- Una solicitud (mismo `externalReference`) no debe producir dos efectos en el proveedor.

Solución:

- Backend para registrar solicitudes y consultarlas por estado.
- Frontend para crearlas, listarlas y filtrarlas.
- Persistencia en SQLite.
- Servicio Windows que envía las pendientes al proveedor y actualiza su estado.

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

Solución .NET 10 (`Prueba.sln`), SQLite con EF Core.

```
src/
├── Prueba.Api                     Backend, ASP.NET Core
│   ├── Controllers
│   │   └── RequestController.cs                     POST/GET /api/requests
│   ├── Middlewares
│   │   └── ExceptionHandlingMiddleware.cs           Excepciones → ProblemDetails (409 / 500)
│   ├── Program.cs                                   Dependencias, CORS, migraciones al iniciar
│   └── Prueba.Api.http                              Peticiones de ejemplo
├── Prueba.Worker                  Servicio Windows
│   ├── ProcessingWorker.cs                          BackgroundService, un ciclo cada PollIntervalSeconds
│   └── Program.cs                                   AddWindowsService, dependencias, migraciones
└── Prueba.Core                    Dominio, persistencia y contratos
    ├── DTOs
    │   └── RequestDTO.cs                            RequestDTO (entrada), RequestResponseDTO (salida)
    ├── Enums
    │   └── RequestStatus.cs                         Pending, Processing, Completed, Failed
    ├── Exceptions
    │   ├── DuplicateExternalReferenceException.cs   409, mismo externalReference (regla 3)
    │   ├── InvalidStatusTransitionException.cs      409, transición no permitida (regla 5)
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
    │   │   └── InitialCreate                        Tabla Request
    │   └── RequestRepository.cs                     Violación del índice único → DuplicateExternalReferenceException
    └── Services
        ├── ExternalProviderSimulation.cs            Proveedor simulado, idempotente por ExternalReference
        ├── RequestService.cs                        Alta, control de duplicados, transiciones de estado
        ├── RequestWorkerService.cs                  Procesa Pending contra el proveedor
        └── WorkerOptions.cs                         WorkerOptions, ProviderOptions

web/                               Frontend, React 18 + Vite
├── src
│   ├── components
│   │   ├── RequestForm.jsx                          Registro de solicitudes
│   │   └── RequestTable.jsx                         Listado, filtro por estado, Refrescar
│   ├── api.js                                       Cliente de /api/requests
│   └── App.jsx
└── vite.config.js                                   Proxy /api → http://localhost:5112

scripts/
├── install-worker-service.ps1                       Instala el Servicio Windows
└── uninstall-worker-service.ps1                     Desinstala el Servicio Windows

tests/
└── Prueba.Tests                   Pruebas, xUnit
    ├── Helpers                                      TestDb, FakeProvider, ApiFactory, FailOnCompletedRequestService
    ├── RequestControllerTests.cs
    ├── RequestRepositoryTests.cs
    ├── RequestWorkerServiceTests.cs
    └── StatusRulesTests.cs
```

## 3. Backend

URL base: `http://localhost:5112`

| Método | Ruta                           | Respuesta                                        |
|--------|--------------------------------|--------------------------------------------------|
| POST   | `/api/requests`                | `201`, solicitud en `Pending`, header `Location` |
| GET    | `/api/requests?status=Pending` | `200`, lista; `status` opcional                  |
| GET    | `/api/requests/{id}`           | `200`, solicitud; `404` si no existe             |

Body del POST:

```json
{
  "externalReference": "OP-1001",
  "customerId": "CUST-01",
  "serviceCode": "INSTALL",
  "description": "Instalación de equipo"
}
```

Errores, en formato ProblemDetails (RFC 7807):

| Código | Causa                                                        |
|--------|--------------------------------------------------------------|
| `400`  | Campos vacíos o demasiado largos; `status` inexistente       |
| `409`  | `externalReference` ya registrado (regla 3)                  |
| `500`  | Error no controlado; el detalle va al log                    |

Duplicados: `RequestService` busca el `externalReference` antes de insertar; el índice único resuelve dos POST simultáneos.

Migraciones: el Backend y el Servicio Windows las aplican al iniciar.

## 4. Servicio Windows

Ciclo, cada `PollIntervalSeconds`:

1. Devuelve a `Pending` las solicitudes en `Processing` por más de `ProcessingTimeoutSeconds`.
2. Lee hasta `BatchSize` solicitudes `Pending`.
3. Toma cada una con un `UPDATE` condicional (`Pending → Processing`, `Attempts + 1`). Si otro proceso ya la tomó, la salta.
4. Llama al proveedor con `ExternalReference` como clave de idempotencia:
   - Éxito: `Completed`.
   - Fallo transitorio: `Pending`, o `Failed` al llegar a `MaxAttempts`.
   - Éxito con fallo al guardar: queda en `Processing` hasta el paso 1.

Transiciones: `Pending → Processing → Completed | Pending | Failed`. `Completed` y `Failed` son finales (regla 5).

Garantía: al menos una vez. No hay transacción entre el proveedor y la base de datos; un reenvío es seguro porque el proveedor es idempotente por `ExternalReference` (regla 3).

Configuración (`src/Prueba.Worker/appsettings.json`):

| Clave                             | Valor | Uso                                    |
|-----------------------------------|-------|----------------------------------------|
| `Worker:PollIntervalSeconds`      | 5     | Segundos entre ciclos                  |
| `Worker:BatchSize`                | 10    | Solicitudes por ciclo                  |
| `Worker:MaxAttempts`              | 3     | Intentos antes de `Failed`             |
| `Worker:ProcessingTimeoutSeconds` | 60    | Límite en `Processing`                 |
| `Provider:FailureRate`            | 0.3   | Probabilidad de fallo del simulado     |
| `Provider:LatencyMs`              | 300   | Latencia del simulado                  |

Hosting: `AddWindowsService` permite ejecutarlo en consola o como servicio (Service Control Manager, Event Log). Ver [Instalar el Servicio Windows](#instalar-el-servicio-windows).

## 5. Frontend

React 18 + Vite. En desarrollo, Vite hace proxy de `/api` al Backend.

- **RequestForm:** registra solicitudes; valida longitudes, evita el doble envío y muestra los errores 400 y 409.
- **RequestTable:** `externalReference`, `customerId`, `serviceCode`, descripción, estado, intentos y última actualización.
- **Filtro por estado** y botón **Refrescar**. Sin recarga automática.

## 6. Pruebas

27 pruebas sobre duplicados, estados y fallos del proveedor. Usan SQLite en memoria con el schema real, porque dependen del índice único y del `UPDATE` condicional.

| Archivo                     | Escenarios                                                                                          |
|-----------------------------|-----------------------------------------------------------------------------------------------------|
| `StatusRulesTests`          | `Completed` y `Failed` son finales; transiciones válidas; salir de `Completed` falla y no persiste  |
| `RequestRepositoryTests`    | Violación del índice único → duplicado; otro error de base de datos no se trata como duplicado      |
| `RequestWorkerServiceTests` | Éxito con un envío; fallo y luego éxito; `Failed` tras `MaxAttempts`; `Completed` no se reenvía; un solo proceso toma la solicitud; fallo al guardar no reenvía; recuperación tras timeout |
| `RequestControllerTests`    | POST → `Pending`; POST repetido → 409; 10 POST simultáneos → una solicitud; campos vacíos → 400; filtro por estado; estado inválido → 400 |
