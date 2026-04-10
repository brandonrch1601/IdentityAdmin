using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Infrastructure.Services;

/// <summary>
/// Almacén thread-safe de la clave pública RSA del emisor de JWT.
/// Singleton: se inicializa una vez en el startup y se puede refrescar.
/// Permite desacoplar la carga de la clave de la configuración de JwtBearer.
/// </summary>
public sealed class JwtSigningKeyProvider
{
    private volatile RsaSecurityKey? _publicKey;
    private readonly object _lock = new();

    public RsaSecurityKey? PublicKey => _publicKey;

    public bool IsInitialized => _publicKey is not null;

    /// <summary>Establece la clave pública RSA cargada desde Azure Key Vault.</summary>
    public void SetPublicKey(RsaSecurityKey key)
    {
        lock (_lock)
        {
            _publicKey = key;
        }
    }

    /// <summary>Retorna la clave pública RSA o lanza si no fue inicializada.</summary>
    public RsaSecurityKey GetPublicKeyOrThrow() =>
        _publicKey ?? throw new InvalidOperationException(
            "JwtSigningKeyProvider no ha sido inicializado. " +
            "Asegúrese de que Azure Key Vault esté configurado correctamente.");
}
