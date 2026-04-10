using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdentityAdministration.Application.Auth.Commands.AuthenticateExternalUser;
using IdentityAdministration.Application.Auth.Queries.GetHomeRealmDiscovery;
using IdentityAdministration.Domain.Common;

namespace IdentityAdministration.API.Controllers;

/// <summary>
/// Endpoint de autenticación multi-tenant basado en OIDC / Token Exchange.
/// No requiere autenticación previa: estos endpoints inician el flujo de login.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Paso 1 — Home Realm Discovery.
    /// Identifica el proveedor de identidad (Google/Microsoft) para el dominio del email.
    /// El frontend usa esta información para redirigir al usuario al proveedor correcto.
    /// </summary>
    /// <param name="email">Correo corporativo del usuario.</param>
    [HttpGet("discovery/{email}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HomeRealmDiscoveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDiscovery(
        string email,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetHomeRealmDiscoveryQuery(email),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error);
    }

    /// <summary>
    /// Paso 2 — Token Exchange.
    /// Valida el ID Token del proveedor externo y emite un JWT interno del SaaS
    /// con los permisos del usuario del tenant.
    /// El usuario debe haber sido provisionado por un administrador previamente.
    /// </summary>
    [HttpPost("exchange")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ExchangeTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Exchange(
        [FromBody] AuthenticateExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error);
    }

    private IActionResult MapError(Error error) => error.Code switch
    {
        "USER_NOT_PROVISIONED" => Unauthorized(new { error.Code, error.Message }),
        "USER_INACTIVE" => StatusCode(StatusCodes.Status403Forbidden, new { error.Code, error.Message }),
        "TENANT_NOT_FOUND" => NotFound(new { error.Code, error.Message }),
        "AUTH_CONFIG_NOT_FOUND" => NotFound(new { error.Code, error.Message }),
        "INVALID_EXTERNAL_TOKEN" => Unauthorized(new { error.Code, error.Message }),
        _ => BadRequest(new { error.Code, error.Message })
    };
}
