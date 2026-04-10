using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdentityAdministration.API.Authorization;
using IdentityAdministration.Application.Users.Commands.AssignRolesToUser;
using IdentityAdministration.Application.Users.Commands.CreateUser;
using IdentityAdministration.Application.Users.Commands.UpdateUserStatus;
using IdentityAdministration.Application.Users.Queries.GetUserById;
using IdentityAdministration.Application.Users.Queries.GetUsers;
using IdentityAdministration.Domain.Common;

namespace IdentityAdministration.API.Controllers;

/// <summary>
/// Gestión de usuarios del tenant. Todos los endpoints requieren permiso USER_ADMIN.
/// El aislamiento por tenant es automático vía Global Query Filters de EF Core.
/// </summary>
[ApiController]
[Route("users")]
[Authorize]
[HasPermission("USER_ADMIN")]
[Produces("application/json")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    /// <summary>Lista paginada de usuarios del tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? statusCode,
        [FromQuery] string? email,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetUsersQuery(statusCode, email, page, pageSize),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    /// <summary>Detalle de un usuario por ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.Error.Code, result.Error.Message });
    }

    /// <summary>
    /// Crea un nuevo usuario en el tenant.
    /// El administrador debe proporcionar el ExternalId (OID/Sub) del usuario en el IdP.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "USER_ALREADY_EXISTS" => Conflict(new { result.Error.Code, result.Error.Message }),
                "EMAIL_DOMAIN_MISMATCH" => BadRequest(new { result.Error.Code, result.Error.Message }),
                "INVALID_ROLE_IDS" => BadRequest(new { result.Error.Code, result.Error.Message }),
                _ => BadRequest(new { result.Error.Code, result.Error.Message })
            };
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            result.Value);
    }

    /// <summary>Activa o desactiva un usuario.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateUserStatusCommand(id, request.NewStatusCode),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : result.Error.Code == "USER_NOT_FOUND"
                ? NotFound(new { result.Error.Code, result.Error.Message })
                : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    /// <summary>Reemplaza todos los roles de un usuario.</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignRoles(
        Guid id,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AssignRolesToUserCommand(id, request.RoleIds),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : result.Error.Code == "USER_NOT_FOUND"
                ? NotFound(new { result.Error.Code, result.Error.Message })
                : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}

// ── Request bodies ────────────────────────────────────────────────────────────
public record UpdateUserStatusRequest(string NewStatusCode);
public record AssignRolesRequest(IReadOnlyList<int> RoleIds);
