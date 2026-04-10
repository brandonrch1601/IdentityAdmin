using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Infrastructure.Services;

/// <summary>
/// Implementación del contexto de tenant para peticiones HTTP autenticadas.
/// Su ciclo de vida es Scoped (una instancia por petición).
/// Poblado por <c>TenantResolutionMiddleware</c> al validar el JWT interno.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private Guid _userId;

    public Guid TenantId => IsAuthenticated
        ? _tenantId
        : throw new InvalidOperationException("El TenantContext no ha sido inicializado.");

    public Guid UserId => IsAuthenticated
        ? _userId
        : throw new InvalidOperationException("El TenantContext no ha sido inicializado.");

    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Inicializa el contexto con los datos del JWT interno ya validado.
    /// Solo puede ser invocado una vez por petición.
    /// </summary>
    public void Initialize(Guid tenantId, Guid userId)
    {
        if (IsAuthenticated)
            throw new InvalidOperationException("El TenantContext ya fue inicializado en esta petición.");

        _tenantId = tenantId;
        _userId = userId;
        IsAuthenticated = true;
    }
}
