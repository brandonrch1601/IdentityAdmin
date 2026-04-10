using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Enums;

namespace IdentityAdministration.Domain.Interfaces.Services;

/// <summary>
/// Valida tokens de identidad emitidos por Google o Microsoft,
/// verificando firma contra los JWKS oficiales del proveedor.
/// </summary>
public interface IExternalTokenValidator
{
    /// <summary>
    /// Valida el ID Token emitido por un proveedor OIDC externo.
    /// </summary>
    /// <param name="idToken">JWT emitido por Google o Microsoft.</param>
    /// <param name="provider">Proveedor de identidad.</param>
    /// <param name="expectedIssuer">Issuer URL configurado en TenantAuthConfig.</param>
    /// <param name="clientId">ClientId esperado en el claim 'aud'.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<ExternalClaimsResult>> ValidateAsync(
        string idToken,
        ProviderType provider,
        string expectedIssuer,
        string clientId,
        CancellationToken cancellationToken = default);
}

/// <summary>Claims extraídos del ID Token válido del proveedor externo.</summary>
public record ExternalClaimsResult(
    /// <summary>'oid' para Microsoft, 'sub' para Google.</summary>
    string ExternalId,
    string Email,
    string? FullName,
    string Issuer);
