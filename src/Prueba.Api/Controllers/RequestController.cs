using Microsoft.AspNetCore.Mvc;
using Prueba.Core.DTOs;
using Prueba.Core.Enums;
using Prueba.Core.Interfaces;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Produces("application/json")]
public class RequestController(IRequestService service) : ControllerBase
{
    /// <summary>Registra una solicitud. Queda en Pending hasta que el Worker la procese.</summary>
    [HttpPost]
    [ProducesResponseType<RequestResponseDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddRequestAsync([FromBody] RequestDTO dto, CancellationToken ct)
    {
        var request = await service.AddRequestAsync(dto, ct);
        return CreatedAtAction(nameof(GetRequestByIdAsync), new { id = request.Id }, RequestResponseDTO.From(request));
    }

    /// <summary>Lista solicitudes, opcionalmente filtradas por estado.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<RequestResponseDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRequestsAsync([FromQuery] RequestStatus? status, CancellationToken ct)
    {
        var requests = await service.GetRequestsAsync(status, ct);
        return Ok(requests.Select(RequestResponseDTO.From));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RequestResponseDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestByIdAsync(int id, CancellationToken ct)
    {
        var request = await service.GetRequestByIdAsync(id, ct);
        if (request is null)
        {
            return NotFound();
        }

        return Ok(RequestResponseDTO.From(request));
    }
}
