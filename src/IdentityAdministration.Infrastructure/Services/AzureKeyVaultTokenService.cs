using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Options;
using IdentityAdministration.Domain.Interfaces.Services;
using IdentityAdministration.Infrastructure.Options;

namespace IdentityAdministration.Infrastructure.Services;

/// <summary>
/// Servicio de emisión de JWT internos usando Azure Key Vault para la firma RSA.
/// La clave privada NUNCA abandona el vault; se usa <see cref="CryptographyClient.SignDataAsync"/>
/// para firmar remotamente con RS256.
/// Requiere <c>DefaultAzureCredential</c> con Workload Identity configurado en AKS.
/// </summary>
public sealed class AzureKeyVaultTokenService : ITokenService
{
    private readonly KeyClient _keyClient;
    private readonly DefaultAzureCredential _credential;
    private readonly JwtOptions _jwtOptions;
    private readonly AzureKeyVaultOptions _kvOptions;

    public AzureKeyVaultTokenService(
        DefaultAzureCredential credential,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AzureKeyVaultOptions> kvOptions)
    {
        _credential = credential;
        _jwtOptions = jwtOptions.Value;
        _kvOptions = kvOptions.Value;
        _keyClient = new KeyClient(new Uri(_kvOptions.VaultUri), credential);
    }

    public async Task<TokenResult> GenerateInternalTokenAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        // 1 — Recuperar la key de AKV (obtiene el kid y la clave pública)
        var keyResponse = await _keyClient.GetKeyAsync(
            _kvOptions.TokenSigningKeyName,
            cancellationToken: cancellationToken);
        var keyVaultKey = keyResponse.Value;
        var kid = keyVaultKey.Properties.Version ?? keyVaultKey.Name;

        // 2 — Construir header y payload del JWT
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(_jwtOptions.ExpirationMinutes);

        var header = new { alg = "RS256", typ = "JWT", kid };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = _jwtOptions.Issuer,
            ["aud"] = _jwtOptions.Audience,
            ["sub"] = userId.ToString(),
            ["tenant_id"] = tenantId.ToString(),
            ["user_id"] = userId.ToString(),
            ["permissions"] = permissions,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N")
        };

        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signingInput = $"{headerB64}.{payloadB64}";
        var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);

        // 3 — Firmar REMOTAMENTE en Azure Key Vault (la clave privada no sale del vault)
        var cryptoClient = new CryptographyClient(keyVaultKey.Id, _credential);
        var signResult = await cryptoClient.SignDataAsync(
            SignatureAlgorithm.RS256,
            signingInputBytes,
            cancellationToken);

        var signature = Base64UrlEncode(signResult.Signature);
        var jwt = $"{signingInput}.{signature}";

        return new TokenResult(jwt, (int)(exp - now).TotalSeconds);
    }

    public async Task<JsonWebKeySetResult> GetPublicKeySetAsync(
        CancellationToken cancellationToken = default)
    {
        var keyResponse = await _keyClient.GetKeyAsync(
            _kvOptions.TokenSigningKeyName,
            cancellationToken: cancellationToken);
        var keyVaultKey = keyResponse.Value;

        // Exportar solo la clave pública RSA
        using var rsa = keyVaultKey.Key.ToRSA(includePrivateParameters: false);
        var rsaParams = rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new JsonWebKeyDto(
            Kty: "RSA",
            Use: "sig",
            Kid: keyVaultKey.Properties.Version ?? keyVaultKey.Name,
            Alg: "RS256",
            N: Base64UrlEncode(rsaParams.Modulus!),
            E: Base64UrlEncode(rsaParams.Exponent!));

        return new JsonWebKeySetResult([jwk]);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
