# Portal de solicitudes y procesamiento en segundo plano

Prueba técnica Full Stack .NET.

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
└── Prueba.Core                    Dominio, persistencia y contratos
    ├── DTOs
    │   └── RequestDTO.cs          RequestDTO (entrada) y RequestResponseDTO (salida)
    ├── Enums
    │   └── RequestStatus.cs       Pending, Processing, Completed, Failed
    ├── Exceptions
    │   ├── DuplicateExternalReferenceException.cs   409: mismo externalReference (regla 3)
    │   └── InvalidStatusTransitionException.cs      409: transición no permitida (regla 5)
    ├── Interfaces
    │   ├── IRequestRepository.cs
    │   └── IRequestService.cs
    ├── Persistence
    │   ├── Entities
    │   │   └── Request.cs
    │   ├── DBContexts
    │   │   └── RequestDBContext.cs                  Índice único en ExternalReference, índice en Status
    │   ├── Migrations
    │   │   └── InitialCreate                        Crea la tabla Request
    │   └── RequestRepository.cs                     Traduce la violación del índice único a DuplicateExternalReferenceException
    └── Services
        └── RequestService.cs                        Alta con control de duplicados y transiciones de estado válidas
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

### Cómo ejecutar

Requisitos: .NET SDK 10.

```
dotnet run --project src/Prueba.Api
```

La base SQLite se crea sola en `data/portal.db` al iniciar (migraciones de EF Core). Para reiniciarla, borrar ese archivo.
Peticiones de ejemplo en `src/Prueba.Api/Prueba.Api.http`.
