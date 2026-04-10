namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Usuario del SaaS vinculado a un proveedor de identidad externo (Users).
/// No almacena contraseñas; la autenticación es delegada a Google/Microsoft.
/// El provisioning es exclusivo del administrador del tenant.
/// </summary>
public sealed class User
{
    private readonly List<UserRole> _userRoles = [];

    private User() { } // EF Core

    /// <summary>
    /// Crea un nuevo usuario. Solo puede ser invocado por el flujo
    /// de administración (CreateUserCommandHandler).
    /// </summary>
    public static User Create(
        Guid tenantId,
        string externalId,
        string email,
        string? fullName,
        int activeStatusId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalId = externalId,
            Email = email.ToLowerInvariant(),
            FullName = fullName,
            StatusId = activeStatusId,
            LicenseSeat = true, // Reservado para esquema de licencias futuro
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Identificador único del proveedor externo:
    /// 'oid' para Microsoft Entra ID, 'sub' para Google.
    /// </summary>
    public string ExternalId { get; private set; } = default!;

    public string Email { get; private set; } = default!;
    public string? FullName { get; private set; }
    public int? StatusId { get; private set; }
    public DateTimeOffset? LastLogin { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Reservado para el esquema de licencias por usuario.
    /// En fase MVP siempre es <c>true</c>. En fases futuras,
    /// CreateUserCommand verificará asientos disponibles antes de activar.
    /// </summary>
    public bool LicenseSeat { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public CatStatus? Status { get; private set; }
    public Tenant? Tenant { get; private set; }
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    // ── Comportamiento ───────────────────────────────────────────────────────
    /// <summary>Registra el timestamp del último login exitoso.</summary>
    public void RecordLogin() => LastLogin = DateTimeOffset.UtcNow;

    /// <summary>Actualiza el estado del usuario (ACTIVE/INACTIVE).</summary>
    public void UpdateStatus(int statusId) => StatusId = statusId;

    /// <summary>Verifica si el usuario tiene el estado activo dado.</summary>
    public bool IsActive(int activeStatusId) => StatusId == activeStatusId;
}
