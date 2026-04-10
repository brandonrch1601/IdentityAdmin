using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Users.Commands.AssignRolesToUser;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Reemplaza todos los roles de un usuario por la nueva lista indicada.
/// Requiere permiso USER_ADMIN.
/// </summary>
public record AssignRolesToUserCommand(
    Guid UserId,
    IReadOnlyList<int> RoleIds)
    : IRequest<Result<bool>>;

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class AssignRolesToUserCommandHandler(
    ITenantContext tenantContext,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IAuditLogRepository auditLogRepository,
    ILogger<AssignRolesToUserCommandHandler> logger)
    : IRequestHandler<AssignRolesToUserCommand, Result<bool>>
{
    private const string UserEntity = "User";

    public async Task<Result<bool>> Handle(
        AssignRolesToUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var operatorId = tenantContext.UserId;

        // Verificar que el usuario exista (Global Query Filter aplica tenant)
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Error.UserNotFound;

        // Verificar que los roles pertenezcan al tenant
        if (request.RoleIds.Count > 0 &&
            !await roleRepository.AllBelongToTenantAsync(tenantId, request.RoleIds, cancellationToken))
            return Error.InvalidRoleIds;

        await userRepository.ReplaceRolesAsync(request.UserId, request.RoleIds, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        var newValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new { roles = request.RoleIds }));

        await auditLogRepository.AddAsync(
            AuditLog.Create(tenantId, operatorId, "ROLE_ASSIGNED", UserEntity,
                entityId: user.Id, newValues: newValues),
            cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Roles de usuario {UserId} actualizados por {OperatorId}: [{Roles}]",
            user.Id, operatorId, string.Join(", ", request.RoleIds));

        return Result<bool>.Success(true);
    }
}

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class AssignRolesToUserCommandValidator : AbstractValidator<AssignRolesToUserCommand>
{
    public AssignRolesToUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El ID del usuario es requerido.");

        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage("La lista de roles no puede ser nula.")
            .Must(r => r.Distinct().Count() == r.Count)
            .WithMessage("La lista de roles no puede contener duplicados.");
    }
}
