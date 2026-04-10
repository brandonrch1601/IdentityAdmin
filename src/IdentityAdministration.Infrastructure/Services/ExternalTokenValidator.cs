using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Enums;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Infrastructure.Services;

/// <summary>
/// Valida tokens de identidad emitidos por Google y Microsoft,
/// descargando y cacheando los JWKS del proveedor.
/// </summary>
public sealed class ExternalTokenValidator(ILogger<ExternalTokenValidator> logger) : IExternalTokenValidator
{
    // Cache de configurationManagers por issuer (thread-safe dict)
    private readonly Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        _configManagers = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _managerLock = new();

    public async Task<Result<ExternalClaimsResult>> ValidateAsync(
        string idToken,
        ProviderType provider,
        string expectedIssuer,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataAddress = BuildMetadataAddress(provider, expectedIssuer);
            var configManager = GetOrCreateConfigManager(metadataAddress);

            var oidcConfig = await configManager
                .GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = expectedIssuer,
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5),
                ValidateIssuerSigningKey = true
            };

            var handler = new JwtSecurityTokenHandler
            {
                // Mantener los claim types originales del JWT (e.g. "email", "oid", "sub")
                // Por defecto JwtSecurityTokenHandler mapea a ClaimTypes.* de .NET,
                // lo que haría que FindFirst("email") no encuentre nada.
                MapInboundClaims = false
            };
            ClaimsPrincipal principal;
            JwtSecurityToken securityToken;

            try
            {
                principal = handler.ValidateToken(idToken, validationParams, out var rawToken);
                securityToken = (JwtSecurityToken)rawToken;
            }
            catch (SecurityTokenException stEx)
            {
                // Intentar refrescar JWKS en caso de rotación de claves
                logger.LogWarning(stEx,
                    "[ExternalTokenValidator] Primera validación fallida, refrescando JWKS — provider={Provider}",
                    provider);

                configManager.RequestRefresh();
                var refreshedConfig = await configManager
                    .GetConfigurationAsync(cancellationToken)
                    .ConfigureAwait(false);

                validationParams.IssuerSigningKeys = refreshedConfig.SigningKeys;
                principal = handler.ValidateToken(idToken, validationParams, out var rawToken2);
                securityToken = (JwtSecurityToken)rawToken2;
            }

            // Extraer claims según el proveedor — usamos FindFirst en vez de FindFirstValue
            // para evitar dependencia en Microsoft.AspNetCore.Components
            var externalId = provider switch
            {
                ProviderType.Microsoft =>
                    principal.FindFirst("oid")?.Value ?? principal.FindFirst("sub")?.Value,
                ProviderType.Google =>
                    principal.FindFirst("sub")?.Value,
                _ => null
            };

            var email =
                principal.FindFirst("email")?.Value ??
                principal.FindFirst("preferred_username")?.Value;

            var givenName = principal.FindFirst("given_name")?.Value ?? string.Empty;
            var familyName = principal.FindFirst("family_name")?.Value ?? string.Empty;
            var fullName =
                principal.FindFirst("name")?.Value ??
                $"{givenName} {familyName}".Trim();

            logger.LogDebug(
                "[ExternalTokenValidator] Claims extraídos — ExternalId={ExternalId} Email={Email} FullName={FullName}",
                externalId, email, fullName);

            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning(
                    "[ExternalTokenValidator] ExternalId o Email vacíos después de extraer claims — " +
                    "oid={Oid} sub={Sub} email={Email} preferred_username={PrefUser}",
                    principal.FindFirst("oid")?.Value,
                    principal.FindFirst("sub")?.Value,
                    principal.FindFirst("email")?.Value,
                    principal.FindFirst("preferred_username")?.Value);
                return Error.InvalidExternalToken;
            }

            return new ExternalClaimsResult(
                ExternalId: externalId,
                Email: email.ToLowerInvariant(),
                FullName: string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                Issuer: securityToken.Issuer);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex,
                "[ExternalTokenValidator] SecurityTokenException — provider={Provider} issuer={Issuer}",
                provider, expectedIssuer);
            return Error.InvalidExternalToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ExternalTokenValidator] Error inesperado — provider={Provider} issuer={Issuer}",
                provider, expectedIssuer);
            return Error.InvalidExternalToken;
        }
    }

    private ConfigurationManager<OpenIdConnectConfiguration> GetOrCreateConfigManager(
        string metadataAddress)
    {
        lock (_managerLock)
        {
            if (!_configManagers.TryGetValue(metadataAddress, out var manager))
            {
                manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true });

                _configManagers[metadataAddress] = manager;
            }
            return manager;
        }
    }

    private static string BuildMetadataAddress(ProviderType provider, string issuer) =>
        provider switch
        {
            ProviderType.Google =>
                "https://accounts.google.com/.well-known/openid-configuration",
            ProviderType.Microsoft =>
                $"{issuer.TrimEnd('/')}/.well-known/openid-configuration",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
}
