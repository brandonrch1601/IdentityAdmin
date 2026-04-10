using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;
using IdentityAdministration.Infrastructure.Options;
using IdentityAdministration.Infrastructure.Persistence;
using IdentityAdministration.Infrastructure.Repositories;
using IdentityAdministration.Infrastructure.Services;
using System.Security.Cryptography;

namespace IdentityAdministration.Infrastructure;

/// <summary>
/// Registra todos los servicios de la capa Infrastructure en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        // ── Opciones tipadas ────────────────────────────────────────────────
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AzureKeyVaultOptions>(
            configuration.GetSection(AzureKeyVaultOptions.SectionName));

        // ── Entity Framework Core + Npgsql ──────────────────────────────────
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3));
        });

        // ── Repositorios ────────────────────────────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        // ── Tenant Context (Scoped) ─────────────────────────────────────────
        services.AddScoped<ITenantContext, TenantContext>();

        // ── JWT Key Provider (Singleton) ────────────────────────────────────
        services.AddSingleton<JwtSigningKeyProvider>();

        // ── Cache en memoria para JWKS externos ────────────────────────────
        services.AddMemoryCache();

        // ── Validador de tokens externos ────────────────────────────────────
        services.AddSingleton<IExternalTokenValidator, ExternalTokenValidator>();

        // ── Token Service: AKV en producción / Local RSA en desarrollo ──────
        if (isDevelopment)
        {
            services.AddSingleton<ITokenService, LocalRsaTokenService>();
        }
        else
        {
            // Workload Identity de AKS: DefaultAzureCredential resuelve automáticamente
            // la Managed Identity del pod via OIDC Federated Credential
            services.AddSingleton<DefaultAzureCredential>();
            services.AddSingleton<ITokenService, AzureKeyVaultTokenService>();
        }

        return services;
    }

    /// <summary>
    /// Inicializa la clave pública de validación JWT al arrancar la aplicación.
    /// En producción carga desde AKV; en desarrollo usa la clave local generada.
    /// </summary>
    public static async Task InitializeJwtSigningKeyAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var keyProvider = serviceProvider.GetRequiredService<JwtSigningKeyProvider>();
        var tokenService = serviceProvider.GetRequiredService<ITokenService>();

        var jwks = await tokenService.GetPublicKeySetAsync(cancellationToken);

        if (jwks.Keys.Count == 0)
            throw new InvalidOperationException(
                "No se pudo obtener la clave pública RSA para validación de JWT.");

        var firstKey = jwks.Keys[0];
        var rsa = RSA.Create();
        rsa.ImportParameters(new System.Security.Cryptography.RSAParameters
        {
            Modulus = Base64UrlDecode(firstKey.N),
            Exponent = Base64UrlDecode(firstKey.E)
        });

        keyProvider.SetPublicKey(new RsaSecurityKey(rsa) { KeyId = firstKey.Kid });
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
