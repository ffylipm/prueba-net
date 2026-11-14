using Microsoft.AspNetCore.Mvc;
using Prueba.Core.Exceptions;

namespace Prueba.Api.Middlewares;

/// <summary>
/// Traduce excepciones de negocio a ProblemDetails (RFC 7807).
/// Los errores no controlados devuelven 500 sin detalles internos; el detalle va al log.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DuplicateExternalReferenceException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, "Solicitud duplicada", ex.Message);
        }
        catch (InvalidStatusTransitionException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, "Transición de estado no permitida", ex.Message);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Error interno",
                "Ocurrió un error inesperado. Intenta de nuevo más tarde.");
        }
    }

    private static Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        }, options: null, contentType: "application/problem+json");
    }
}
