using System.Text.Json;

namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Registro de auditoría inmutable (Audit_Logs).
/// Capta intentos de login, cambios en permisos y gestión de usuarios.
/// Una vez creado, ningún campo puede ser modificado.
/// </summary>
public sealed class AuditLog
{
    private AuditLog() { } // EF Core

    /// <summary>
    /// Crea un nuevo registro de auditoría.
    /// </summary>
    public static AuditLog Create(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityName,
        Guid? entityId = null,
        JsonDocument? oldValues = null,
        JsonDocument? newValues = null,
        string? ipAddress = null) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            Timestamp = DateTimeOffset.UtcNow
        };

    /// <summary>BIGSERIAL generado por PostgreSQL.</summary>
    public long Id { get; private set; }

    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }

    /// <summary>Acción ejecutada. Ej: LOGIN, LOGIN_FAILED, USER_CREATED, ROLE_ASSIGNED.</summary>
    public string Action { get; private set; } = default!;

    /// <summary>Entidad afectada. Ej: User, Role, Permission.</summary>
    public string EntityName { get; private set; } = default!;

    public Guid? EntityId { get; private set; }

    /// <summary>Estado anterior de la entidad (JSONB). Nulo si es una creación.</summary>
    public JsonDocument? OldValues { get; private set; }

    /// <summary>Estado nuevo de la entidad (JSONB). Nulo si es una eliminación.</summary>
    public JsonDocument? NewValues { get; private set; }

    /// <summary>Dirección IP del cliente (IPv4 o IPv6).</summary>
    public string? IpAddress { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }
}
