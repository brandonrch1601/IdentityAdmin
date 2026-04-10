using MediatR;
using IdentityAdministration.Application.Roles.Commands;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Roles.Queries;

// ── GetRoles ─────────────────────────────────────────────────────────────────

public record GetRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;

internal sealed class GetRolesQueryHandler(
    ITenantContext tenantContext,
    IRoleRepository roleRepository)
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetByTenantIdAsync(
            tenantContext.TenantId, cancellationToken);

        var dtos = roles.Select(r => new RoleDto(
            r.Id, r.TenantId, r.Name, r.Description,
            r.RolePermissions
                .Select(rp => new PermissionDto(
                    rp.Permission!.Id,
                    rp.Permission.Code,
                    rp.Permission.Description))
                .ToList()))
            .ToList();

        return Result<IReadOnlyList<RoleDto>>.Success(dtos);
    }
}

// ── GetPermissions ───────────────────────────────────────────────────────────

public record GetPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionDto>>>;

internal sealed class GetPermissionsQueryHandler(IPermissionRepository permissionRepository)
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionRepository.GetAllAsync(cancellationToken);

        var dtos = permissions
            .Select(p => new PermissionDto(p.Id, p.Code, p.Description))
            .ToList();

        return Result<IReadOnlyList<PermissionDto>>.Success(dtos);
    }
}
