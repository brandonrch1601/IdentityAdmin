using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Users.Commands.CreateUser;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string? FullName,
    string ExternalId,
    string StatusCode,
    DateTimeOffset? LastLogin,
    DateTimeOffset CreatedAt,
    bool LicenseSeat,
    IReadOnlyList<int> RoleIds);

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Crea un nuevo usuario en el tenant del administrador autenticado.
/// Requiere permiso USER_ADMIN. No es autoservicio.
/// </summary>
public record CreateUserCommand(
    string Email,
    string? FullName,
    /// <summary>OID (Microsoft) o Sub (Google) del usuario en el IdP externo.</summary>
    string ExternalId,
    IReadOnlyList<int> RoleIds)
    : IRequest<Result<UserDto>>;

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class CreateUserCommandHandler(
    ITenantContext tenantContext,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IAuditLogRepository auditLogRepository,
    ILogger<CreateUserCommandHandler> logger)
    : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private const string UserEntity = "User";
    private const string UserActiveCode = "ACTIVE";
    private const string UserGroupName = "USER";

    public async Task<Result<UserDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var operatorUserId = tenantContext.UserId;

        // 1 — Validar que el dominio del email coincida con el tenant
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Error.TenantNotFound;

        var emailDomain = request.Email[(request.Email.IndexOf('@') + 1)..].ToLowerInvariant();
        if (!emailDomain.Equals(tenant.DomainName, StringComparison.OrdinalIgnoreCase))
            return Error.EmailDomainMismatch;

        // 2 — Verificar duplicados
        if (await userRepository.ExistsByExternalIdAsync(request.ExternalId, cancellationToken))
            return Error.UserAlreadyExists;

        if (await userRepository.ExistsByEmailAsync(tenantId, request.Email, cancellationToken))
            return Error.UserAlreadyExists;

        // 3 — Validar que los roles existan y pertenezcan al tenant
        if (request.RoleIds.Count > 0 &&
            !await roleRepository.AllBelongToTenantAsync(tenantId, request.RoleIds, cancellationToken))
            return Error.InvalidRoleIds;

        // 4 — Obtener el StatusId de USER/ACTIVE
        // Los IDs de Cat_Statuses son seriales; resolvemos por búsqueda en dominio
        // La Infrastructure layer puede hacer un SELECT en Cat_Statuses
        // Para evitar acoplar la Application a EF, pasamos un bien-conocido status code
        // y la Infrastructure resuelve el Id. Usamos convención: 3 = USER/ACTIVE (del seed).
        // TODO: En producción, usar ICatStatusRepository para resolver dinámicamente.
        const int activeStatusId = 3; // USER ACTIVE según seed de sql

        // 5 — Crear el usuario
        var user = User.Create(
            tenantId: tenantId,
            externalId: request.ExternalId,
            email: request.Email,
            fullName: request.FullName,
            activeStatusId: activeStatusId);

        await userRepository.AddAsync(user, cancellationToken);

        // 6 — Asignar roles
        if (request.RoleIds.Count > 0)
            await userRepository.ReplaceRolesAsync(user.Id, request.RoleIds, cancellationToken);

        await userRepository.SaveChangesAsync(cancellationToken);

        // 7 — Audit log
        var newValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = user.Id,
                email = user.Email,
                tenantId = user.TenantId,
                roles = request.RoleIds
            }));

        await auditLogRepository.AddAsync(
            AuditLog.Create(tenantId, operatorUserId, "USER_CREATED", UserEntity,
                entityId: user.Id, newValues: newValues),
            cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Usuario {UserId} ({Email}) creado en tenant {TenantId} por {OperatorId}",
            user.Id, user.Email, tenantId, operatorUserId);

        return new UserDto(
            user.Id, user.TenantId, user.Email, user.FullName, user.ExternalId,
            UserActiveCode, user.LastLogin, user.CreatedAt, user.LicenseSeat,
            request.RoleIds);
    }
}

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El formato del correo no es válido.")
            .MaximumLength(255).WithMessage("El correo no puede exceder 255 caracteres.");

        RuleFor(x => x.ExternalId)
            .NotEmpty().WithMessage("El ID externo (OID/Sub) es requerido.")
            .MaximumLength(255).WithMessage("El ID externo no puede exceder 255 caracteres.");

        RuleFor(x => x.FullName)
            .MaximumLength(255).WithMessage("El nombre completo no puede exceder 255 caracteres.")
            .When(x => x.FullName is not null);

        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage("La lista de roles no puede ser nula.")
            .Must(r => r.Distinct().Count() == r.Count)
            .WithMessage("La lista de roles no puede contener duplicados.");
    }
}
