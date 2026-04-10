namespace IdentityAdministration.Domain.Interfaces.Services;

/// <summary>
/// Servicio de emisión de JWT internos del SaaS, firmados con clave RSA en Azure Key Vault.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Genera un JWT interno firmado con RS256 que contiene los claims
    /// <c>tenant_id</c>, <c>user_id</c> y <c>permissions</c>.
    /// La firma se realiza remotamente en Azure Key Vault.
    /// </summary>
    Task<TokenResult> GenerateInternalTokenAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna el JSON Web Key Set (JWKS) con la clave pública RSA,
    /// para publicar en GET /.well-known/jwks.json.
    /// </summary>
    Task<JsonWebKeySetResult> GetPublicKeySetAsync(CancellationToken cancellationToken = default);
}

/// <summary>JWT generado con su tiempo de expiración en segundos.</summary>
public record TokenResult(string AccessToken, int ExpiresIn);

/// <summary>JWKS conteniendo las claves públicas del emisor.</summary>
public record JsonWebKeySetResult(IReadOnlyList<JsonWebKeyDto> Keys);

/// <summary>Representación de una clave pública RSA en formato JWK.</summary>
public record JsonWebKeyDto(
    string Kty,
    string Use,
    string Kid,
    string Alg,
    string N,
    string E);
