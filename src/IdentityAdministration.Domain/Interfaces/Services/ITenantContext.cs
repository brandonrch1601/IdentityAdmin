namespace IdentityAdministration.Domain.Interfaces.Services;

/// <summary>
/// Contexto del tenant autenticado, poblado por TenantResolutionMiddleware.
/// Es un servicio Scoped cuya vida útil coincide con la petición HTTP.
/// </summary>
public interface ITenantContext
{
    /// <summary>ID del tenant del usuario autenticado.</summary>
    Guid TenantId { get; }

    /// <summary>ID del usuario autenticado.</summary>
    Guid UserId { get; }

    /// <summary>
    /// Indica si la petición está autenticada con un JWT interno válido.
    /// Cuando es <c>false</c>, los Global Query Filters de EF Core
    /// no aplican restricción por tenant (used in login flow).
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>Inicializa el contexto tras validar el JWT interno.</summary>
    void Initialize(Guid tenantId, Guid userId);
}
