using System.Security.Claims;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.API.Middleware;

/// <summary>
/// Middleware que extrae el <c>tenant_id</c> y <c>user_id</c> del JWT interno validado
/// e inicializa el <see cref="ITenantContext"/> scoped para la petición actual.
/// Debe ejecutarse DESPUÉS de <c>UseAuthentication()</c>.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirstValue("tenant_id");
            var userIdClaim = context.User.FindFirstValue("user_id");

            if (Guid.TryParse(tenantIdClaim, out var tenantId) &&
                Guid.TryParse(userIdClaim, out var userId))
            {
                tenantContext.Initialize(tenantId, userId);
            }
        }

        await next(context);
    }
}

/// <summary>
/// Middleware global de manejo de excepciones.
/// Convierte exceptions conocidas en respuestas ProblemDetails (RFC 7807)
/// y las desconocidas en un 500 genérico para no exponer detalles internos.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (FluentValidation.ValidationException validationEx)
        {
            logger.LogWarning(
                "Validación fallida en {Path}: {Errors}",
                context.Request.Path,
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage)));

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Uno o más errores de validación ocurrieron.",
                status = 400,
                errors
            });
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Ha ocurrido un error interno en el servidor.",
                status = 500,
                traceId = context.TraceIdentifier
            });
        }
    }
}
