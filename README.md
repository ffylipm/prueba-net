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
│   └── Program.cs
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
    └── Persistence
        ├── Entities
        │   └── Request.cs
        ├── DBContexts
        │   └── RequestDBContext.cs                  Índice único en ExternalReference, índice en Status
        └── Migrations
            └── InitialCreate                        Crea la tabla Request
```
