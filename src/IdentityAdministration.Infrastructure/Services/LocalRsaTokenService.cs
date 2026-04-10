using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using IdentityAdministration.Domain.Interfaces.Services;
using IdentityAdministration.Infrastructure.Options;

namespace IdentityAdministration.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ITokenService"/> para entornos de desarrollo local.
/// Genera y mantiene un par de claves RSA efímero en memoria.
/// NO debe ser usada en producción ni en AKS.
/// </summary>
public sealed class LocalRsaTokenService : ITokenService, IDisposable
{
    private readonly RSA _rsa;
    private readonly string _kid;
    private readonly JwtOptions _jwtOptions;

    public LocalRsaTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _rsa = RSA.Create(2048);
        _kid = Guid.NewGuid().ToString("N")[..8]; // kid corto para desarrollo
    }

    public Task<TokenResult> GenerateInternalTokenAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(_jwtOptions.ExpirationMinutes);

        var header = new { alg = "RS256", typ = "JWT", kid = _kid };
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

        var signature = _rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var jwt = $"{signingInput}.{Base64UrlEncode(signature)}";
        return Task.FromResult(new TokenResult(jwt, (int)(exp - now).TotalSeconds));
    }

    public Task<JsonWebKeySetResult> GetPublicKeySetAsync(
        CancellationToken cancellationToken = default)
    {
        var rsaParams = _rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonWebKeyDto(
            Kty: "RSA",
            Use: "sig",
            Kid: _kid,
            Alg: "RS256",
            N: Base64UrlEncode(rsaParams.Modulus!),
            E: Base64UrlEncode(rsaParams.Exponent!));

        return Task.FromResult(new JsonWebKeySetResult([jwk]));
    }

    public void Dispose() => _rsa.Dispose();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
