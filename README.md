# Portal de solicitudes y procesamiento en segundo plano

Prueba técnica Full Stack .NET.

## Contenido

- [Cómo ejecutar](#cómo-ejecutar)
  - [Probar el flujo](#probar-el-flujo)
  - [Reiniciar la base de datos](#reiniciar-la-base-de-datos)
  - [Instalar el Servicio Windows](#instalar-el-servicio-windows)
- [El problema](#el-problema)
- [1. Schema](#1-schema)
- [2. Estructura de la solución](#2-estructura-de-la-solución)
- [3. Backend](#3-backend)
- [4. Servicio en segundo plano](#4-servicio-en-segundo-plano)
- [5. Frontend](#5-frontend)
- [6. Pruebas](#6-pruebas)
- [7. Decisiones técnicas](#7-decisiones-técnicas)
- [8. Supuestos](#8-supuestos)
- [9. Revisión de código legado](#9-revisión-de-código-legado)
- [10. Uso de IA](#10-uso-de-ia)
- [11. Pendiente](#11-pendiente)

## Cómo ejecutar

Requisitos: .NET SDK 10, Node.js 20+.

1. Backend (`http://localhost:5112`):

   ```
   dotnet run --project src/Prueba.Api
   ```

2. Servicio en segundo plano:

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

Con el Backend y el servicio en segundo plano detenidos, borrar `data/portal.db`. Se crea de nuevo al iniciar el Backend.

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
- Servicio en segundo plano, instalable como Servicio Windows, que envía las pendientes al proveedor y actualiza su estado.

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
├── Prueba.Worker                  Servicio en segundo plano
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

Migraciones: el Backend y el servicio en segundo plano las aplican al iniciar.

## 4. Servicio en segundo plano

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

## 7. Decisiones técnicas

### D1. Duplicados por `externalReference`

- **Contexto:** los usuarios reintentan cuando perciben lentitud (regla 3).
- **Alternativas:** devolver la solicitud existente con `200` (POST idempotente); confiar solo en el índice único.
- **Decisión:** `RequestService` busca el `externalReference` antes de insertar y el índice único resuelve la carrera entre dos POST. El duplicado responde `409`.
- **Trade-offs:** un reintento legítimo recibe `409` aunque la solicitud sí se creó; el cliente la encuentra con `GET /api/requests`.
- **Riesgos:** ninguno de duplicado en base de datos; solo la violación del índice de `ExternalReference` se traduce a `409`, cualquier otro error de base de datos responde `500`.
- **Validación:** `RequestControllerTests` (POST repetido, 10 POST simultáneos) y `RequestRepositoryTests`.

### D2. Envío al proveedor sin efectos duplicados

- **Contexto:** no hay transacción posible entre el proveedor externo y la base de datos.
- **Alternativas:** marcar `Completed` antes de enviar (como máximo una vez: puede perder envíos); patrón outbox; transacción distribuida.
- **Decisión:** al menos una vez con proveedor idempotente por `ExternalReference`. La solicitud se toma con un `UPDATE` condicional (`Pending → Processing`, `Attempts + 1`); un fallo al guardar después de enviar deja la solicitud en `Processing` y solo se reintenta tras `ProcessingTimeoutSeconds`.
- **Trade-offs:** depende de que el proveedor real acepte una clave de idempotencia.
- **Riesgos:** si el proveedor tarda más que `ProcessingTimeoutSeconds`, la solicitud puede reenviarse mientras el primer envío sigue en curso; el timeout debe ser mayor que el del proveedor.
- **Validación:** `RequestWorkerServiceTests` (un solo proceso toma la solicitud, fallo al guardar no reenvía, recuperación tras timeout).

### D3. Fallos transitorios

- **Contexto:** la regla 4 no define qué pasa después de un fallo.
- **Alternativas:** `Failed` al primer fallo; reintentos con backoff exponencial.
- **Decisión:** vuelve a `Pending` y se reintenta en el siguiente ciclo; `Failed` al llegar a `MaxAttempts` (3).
- **Trade-offs:** sin backoff; los reintentos siguen el intervalo fijo del ciclo.
- **Riesgos:** una caída larga del proveedor deja varias solicitudes en `Failed`, que es final.
- **Validación:** `RequestWorkerServiceTests` (fallo y luego éxito, `Failed` tras 3 intentos).

### D4. Transiciones de estado (regla 5)

- **Contexto:** una solicitud `Completed` no puede volver a un estado anterior.
- **Alternativas:** métodos de transición en la entidad; librería de máquina de estados.
- **Decisión:** tabla de transiciones permitidas en `RequestService`; `UpdateRequestStatusAsync` es el único punto que cambia el estado. `Completed` y `Failed` son finales.
- **Trade-offs:** la regla vive en el servicio y no en la entidad.
- **Riesgos:** código que asigne `Status` directamente sin pasar por el servicio.
- **Validación:** `StatusRulesTests`.

### D5. SQLite con migraciones de EF Core

- **Contexto:** persistencia real sin instalar un motor de base de datos.
- **Alternativas:** SQL Server LocalDB, SQL Server, MongoDB.
- **Decisión:** SQLite en `data/portal.db`; el Backend y el servicio en segundo plano aplican las migraciones al iniciar.
- **Trade-offs:** cero instalación; SQLite no aplica longitudes máximas (las valida `RequestDTO`) y admite un solo escritor a la vez.
- **Riesgos:** bloqueos de escritura con mucha carga; en producción las migraciones se aplicarían con un script (`dotnet ef migrations script --idempotent`) y no al iniciar.
- **Validación:** la base se crea sola al iniciar el Backend; las pruebas usan el mismo schema en memoria.

### D6. Servicio en segundo plano como proceso separado

- **Contexto:** el PDF pide poder ejecutarlo como Windows Service.
- **Alternativas:** `BackgroundService` dentro del Backend.
- **Decisión:** proyecto `Prueba.Worker` con `AddWindowsService`, script de instalación.
- **Trade-offs:** un proyecto y un proceso más.
- **Riesgos:** la instalación requiere permisos de administrador; el servicio necesita la ruta absoluta de la base (la configura el script).
- **Validación:** ejecución con `dotnet run` contra el Backend; el script publica y configura el servicio.

### D7. `Id` entero autoincremental

- **Contexto:** identificador técnico de la solicitud; la identidad de negocio es `ExternalReference`.
- **Alternativas:** `Guid`.
- **Decisión:** `int` autoincremental.
- **Trade-offs:** más simple y legible; los ids son consecutivos y se pueden recorrer en `GET /api/requests/{id}`, aceptable sin autenticación en esta prueba.

### SQL vs NoSQL

Con MongoDB, la regla 3 se apoyaría en un índice único sobre `externalReference` y en la misma búsqueda previa en el servicio; el duplicado se detectaría por el código de error `11000`. No habría migraciones: el esquema vive en el código, con un índice en `status` para el filtro. La toma de una solicitud sería un `findOneAndUpdate` con filtro por estado, atómico por documento, que basta para este flujo. Se pierden las restricciones relacionales y las consultas SQL ad hoc.

## 8. Supuestos

- **A1.** Un POST con un `externalReference` existente responde `409`; no devuelve la solicitud existente.
- **A2.** Un fallo transitorio devuelve la solicitud a `Pending`; tras 3 intentos pasa a `Failed`.
- **A3.** `Failed` es final, igual que `Completed`.
- **A4.** `externalReference` es único en todo el sistema, no por cliente.
- **A5.** El proveedor real acepta `externalReference` como clave de idempotencia.
- **A6.** El frontend no se actualiza solo; el botón Refrescar muestra el avance del servicio en segundo plano.
- **A7.** Sin autenticación, como permite el PDF.

## 9. Revisión de código legado

### Fragmento A (WebForms)

**Riesgo:** inyección SQL. `txtCustomer.Text` se concatena en la consulta; un valor como `' OR 1=1 --` devuelve todas las filas.

**Cambio mínimo:** consulta parametrizada.

```csharp
protected void btnSearch_Click(object sender, EventArgs e)
{
    const string sql = "SELECT Id, Customer, Status FROM ServiceRequests WHERE Customer = @customer";

    using var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["Main"].ConnectionString);
    using var cmd = new SqlCommand(sql, cn);
    cmd.Parameters.Add("@customer", SqlDbType.NVarChar, 100).Value = txtCustomer.Text.Trim();

    cn.Open();
    using var reader = cmd.ExecuteReader();
    GridView1.DataSource = reader;
    GridView1.DataBind();
}
```

También se libera el `SqlCommand` y el `DataReader`.

### Fragmento B (ASP.NET MVC)

**Riesgos:**

- `Find` puede devolver `null`: `NullReferenceException` y `500`.
- `status` se asigna sin validar: acepta cualquier texto y permite que una solicitud `Completed` vuelva atrás (regla 5).
- Sin `[ValidateAntiForgeryToken]`: expuesto a CSRF.
- Devuelve la entidad completa.

**Cambio mínimo:**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult UpdateStatus(int id, string status)
{
    var request = db.ServiceRequests.Find(id);
    if (request == null)
    {
        return HttpNotFound();
    }

    var allowed = new[] { "Pending", "Processing", "Completed", "Failed" };
    if (!allowed.Contains(status))
    {
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Estado no válido.");
    }

    if (request.Status == "Completed")
    {
        return new HttpStatusCodeResult(HttpStatusCode.Conflict, "Una solicitud Completed no cambia de estado.");
    }

    request.Status = status;
    db.SaveChanges();
    return Json(new { request.Id, request.Status });
}
```

## 10. Uso de IA

- **Para qué:** Claude Code para generar la estructura de la solución, borradores de código, pruebas y el README.
- **Cómo se verificó:** compilación sin advertencias, 27 pruebas automatizadas y ejecución manual del Backend, el servicio en segundo plano y el frontend juntos.
- **Qué se corrigió o rechazó:**
  - El `catch` de `DbUpdateException` trataba cualquier error de base de datos como duplicado; se limitó a la violación del índice único.
  - Un fallo al guardar `Completed` después de enviar devolvía la solicitud a `Pending` y provocaba un reenvío; se separó el manejo de fallos del proveedor y de la base de datos.
  - `Id` pasó de `Guid` a `int` autoincremental.
  - Se quitó la recarga automática del frontend, que el PDF no pide.
  - Se quitó una clase de registro de dependencias compartida en favor de registros explícitos en cada `Program.cs`.
  - Ajustes de nombres de métodos y de estilo (llaves en todos los bloques).

## 11. Pendiente

- **Estado actual:** todos los componentes del PDF están implementados y probados.
- **Decisión pendiente:** si un POST duplicado debe responder `200` con la solicitud existente en lugar de `409` (A1).
- **Siguiente paso:** backoff exponencial en los reintentos y una consulta de estado al proveedor antes de reenviar una solicitud recuperada por timeout.