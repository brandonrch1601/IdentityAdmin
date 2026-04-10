using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.API.Controllers;

/// <summary>
/// Expone la clave pública RSA en formato JWKS para que otros microservicios
/// puedan validar los JWT internos del SaaS sin comunicarse con este servicio.
/// </summary>
[ApiController]
[Route(".well-known")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class WellKnownController(ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Retorna el JSON Web Key Set (JWKS) con la clave pública RSA del emisor.
    /// Endpoint estándar OIDC para validación de firmas JWT.
    /// </summary>
    [HttpGet("jwks.json")]
    [ResponseCache(Duration = 3600)] // Cachear 1 hora; la clave pública cambia raramente
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJwks(CancellationToken cancellationToken)
    {
        var jwks = await tokenService.GetPublicKeySetAsync(cancellationToken);
        return Ok(new { keys = jwks.Keys });
    }
}

/// <summary>
/// Endpoint de salud del servicio para probes de Kubernetes (liveness y readiness).
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() =>
        Ok(new
        {
            status = "Healthy",
            service = "IdentityAdministration",
            timestamp = DateTimeOffset.UtcNow
        });
}
