using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdentityAdministration.API.Authorization;
using IdentityAdministration.Application.Roles.Commands;
using IdentityAdministration.Application.Roles.Queries;

namespace IdentityAdministration.API.Controllers;

/// <summary>Gestión de roles del tenant. Requiere permiso USER_ADMIN.</summary>
[ApiController]
[Route("roles")]
[Authorize]
[HasPermission("USER_ADMIN")]
[Produces("application/json")]
public sealed class RolesController(IMediator mediator) : ControllerBase
{
    /// <summary>Lista todos los roles del tenant con sus permisos asignados.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRolesQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    /// <summary>Crea un nuevo rol en el tenant.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Code == "ROLE_ALREADY_EXISTS"
                ? Conflict(new { result.Error.Code, result.Error.Message })
                : BadRequest(new { result.Error.Code, result.Error.Message });
        }

        return CreatedAtAction(nameof(GetRoles), result.Value);
    }

    /// <summary>Reemplaza los permisos asignados a un rol.</summary>
    [HttpPut("{id:int}/permissions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePermissions(
        int id,
        [FromBody] UpdatePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateRolePermissionsCommand(id, request.PermissionIds),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : result.Error.Code == "ROLE_NOT_FOUND"
                ? NotFound(new { result.Error.Code, result.Error.Message })
                : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}

/// <summary>Catálogo global de permisos del sistema.</summary>
[ApiController]
[Route("permissions")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    /// <summary>Lista todos los permisos disponibles en el sistema.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPermissionsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}

public record UpdatePermissionsRequest(IReadOnlyList<int> PermissionIds);
