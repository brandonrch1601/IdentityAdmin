using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Users.Commands.UpdateUserStatus;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Activa o desactiva un usuario del tenant. Requiere permiso USER_ADMIN.
/// </summary>
public record UpdateUserStatusCommand(
    Guid UserId,
    /// <summary>Código de estado destino: "ACTIVE" o "INACTIVE".</summary>
    string NewStatusCode)
    : IRequest<Result<bool>>;

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class UpdateUserStatusCommandHandler(
    ITenantContext tenantContext,
    IUserRepository userRepository,
    IAuditLogRepository auditLogRepository,
    ILogger<UpdateUserStatusCommandHandler> logger)
    : IRequestHandler<UpdateUserStatusCommand, Result<bool>>
{
    // IDs del seed de Cat_Statuses para USER
    private const int ActiveStatusId = 3;
    private const int InactiveStatusId = 4;
    private const string UserEntity = "User";

    public async Task<Result<bool>> Handle(
        UpdateUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var operatorId = tenantContext.UserId;

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Error.UserNotFound;

        // Global Query Filter garantiza que el usuario pertenece al tenant
        var oldStatusCode = user.Status?.Code ?? "UNKNOWN";

        var newStatusId = request.NewStatusCode.ToUpperInvariant() switch
        {
            "ACTIVE" => ActiveStatusId,
            "INACTIVE" => InactiveStatusId,
            _ => -1
        };

        if (newStatusId == -1)
            return Error.Unknown;

        user.UpdateStatus(newStatusId);
        await userRepository.UpdateAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        // Audit log con old/new values
        var oldValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new { status = oldStatusCode }));
        var newValues = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new { status = request.NewStatusCode.ToUpperInvariant() }));

        await auditLogRepository.AddAsync(
            AuditLog.Create(tenantId, operatorId, "USER_STATUS_CHANGED", UserEntity,
                entityId: user.Id, oldValues: oldValues, newValues: newValues),
            cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Estado de usuario {UserId} cambiado de {Old} a {New} por {OperatorId}",
            user.Id, oldStatusCode, request.NewStatusCode, operatorId);

        return Result<bool>.Success(true);
    }
}

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    private static readonly string[] AllowedCodes = ["ACTIVE", "INACTIVE"];

    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El ID del usuario es requerido.");

        RuleFor(x => x.NewStatusCode)
            .NotEmpty().WithMessage("El código de estado es requerido.")
            .Must(c => AllowedCodes.Contains(c.ToUpperInvariant()))
            .WithMessage("El estado debe ser ACTIVE o INACTIVE.");
    }
}
