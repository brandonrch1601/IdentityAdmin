using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Roles.Commands;

// ── DTOs de Roles ────────────────────────────────────────────────────────────

public record RoleDto(
    int Id,
    Guid TenantId,
    string Name,
    string? Description,
    IReadOnlyList<PermissionDto> Permissions);

public record PermissionDto(int Id, string Code, string Description);

// ── CreateRole ───────────────────────────────────────────────────────────────

public record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<int> PermissionIds)
    : IRequest<Result<RoleDto>>;

internal sealed class CreateRoleCommandHandler(
    ITenantContext tenantContext,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IAuditLogRepository auditLogRepository,
    ILogger<CreateRoleCommandHandler> logger)
    : IRequestHandler<CreateRoleCommand, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var operatorId = tenantContext.UserId;

        if (await roleRepository.ExistsByNameAsync(tenantId, request.Name, cancellationToken))
            return Error.RoleAlreadyExists;

        if (request.PermissionIds.Count > 0 &&
            !await permissionRepository.AllExistAsync(request.PermissionIds, cancellationToken))
            return Error.InvalidPermissionIds;

        var role = Role.Create(tenantId, request.Name, request.Description);
        await roleRepository.AddAsync(role, cancellationToken);

        if (request.PermissionIds.Count > 0)
            await roleRepository.ReplacePermissionsAsync(role.Id, request.PermissionIds, cancellationToken);

        await roleRepository.SaveChangesAsync(cancellationToken);

        var newValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                roleId = role.Id,
                name = role.Name,
                permissions = request.PermissionIds
            }));

        await auditLogRepository.AddAsync(
            AuditLog.Create(tenantId, operatorId, "ROLE_CREATED", "Role",
                entityId: null, newValues: newValues),
            cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rol '{Name}' creado en tenant {TenantId}", role.Name, tenantId);

        return new RoleDto(role.Id, role.TenantId, role.Name, role.Description, []);
    }
}

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es requerido.")
            .MaximumLength(100).WithMessage("El nombre del rol no puede exceder 100 caracteres.")
            .Matches(@"^[A-Za-z0-9_\-\s]+$").WithMessage("El nombre del rol solo puede contener letras, números, guiones y espacios.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.")
            .When(x => x.Description is not null);

        RuleFor(x => x.PermissionIds)
            .NotNull()
            .Must(p => p.Distinct().Count() == p.Count)
            .WithMessage("La lista de permisos no puede contener duplicados.");
    }
}

// ── UpdateRolePermissions ────────────────────────────────────────────────────

public record UpdateRolePermissionsCommand(
    int RoleId,
    IReadOnlyList<int> PermissionIds)
    : IRequest<Result<bool>>;

internal sealed class UpdateRolePermissionsCommandHandler(
    ITenantContext tenantContext,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IAuditLogRepository auditLogRepository)
    : IRequestHandler<UpdateRolePermissionsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.TenantId != tenantId)
            return Error.RoleNotFound;

        if (request.PermissionIds.Count > 0 &&
            !await permissionRepository.AllExistAsync(request.PermissionIds, cancellationToken))
            return Error.InvalidPermissionIds;

        await roleRepository.ReplacePermissionsAsync(request.RoleId, request.PermissionIds, cancellationToken);
        await roleRepository.SaveChangesAsync(cancellationToken);

        var newValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new { permissions = request.PermissionIds }));

        await auditLogRepository.AddAsync(
            AuditLog.Create(tenantId, tenantContext.UserId, "ROLE_PERMISSIONS_UPDATED", "Role",
                newValues: newValues),
            cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal sealed class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("El ID del rol es requerido.");
        RuleFor(x => x.PermissionIds).NotNull()
            .Must(p => p.Distinct().Count() == p.Count)
            .WithMessage("La lista de permisos no puede contener duplicados.");
    }
}
