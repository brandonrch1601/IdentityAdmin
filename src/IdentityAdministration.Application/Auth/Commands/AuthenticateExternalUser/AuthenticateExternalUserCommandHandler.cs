using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Enums;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Auth.Commands.AuthenticateExternalUser;

/// <summary>
/// Orquesta el flujo de Token Exchange:
/// 1. Decodifica el JWT externo para obtener email e issuer.
/// 2. Resuelve el tenant y la configuración de auth.
/// 3. Valida la firma del token contra los JWKS del proveedor.
/// 4. Verifica que el usuario esté provisionado y activo.
/// 5. Resuelve los permisos y emite el JWT interno del SaaS.
/// </summary>
internal sealed class AuthenticateExternalUserCommandHandler(
    IExternalTokenValidator externalTokenValidator,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    IAuditLogRepository auditLogRepository,
    ITokenService tokenService,
    ILogger<AuthenticateExternalUserCommandHandler> logger)
    : IRequestHandler<AuthenticateExternalUserCommand, Result<ExchangeTokenResponseDto>>
{
    private const string UserEntity = "User";
    private const string LoginAction = "LOGIN";
    private const string LoginFailedAction = "LOGIN_FAILED";
    private const string UserInactiveCode = "INACTIVE";

    public async Task<Result<ExchangeTokenResponseDto>> Handle(
        AuthenticateExternalUserCommand request,
        CancellationToken cancellationToken)
    {
        // 1 — Parsear proveedor
        if (!Enum.TryParse<ProviderType>(request.Provider, ignoreCase: true, out var provider))
            return Error.InvalidExternalToken;

        // 2 — Decodificar el JWT sin validar para extraer email e issuer
        var preDecoded = PreDecodeJwtPayload(request.IdToken);
        if (preDecoded is null)
            return Error.InvalidExternalToken;

        var (email, rawIssuer) = preDecoded.Value;
        var domain = ExtractDomain(email);

        // 3 — Resolver tenant por dominio
        var tenant = await tenantRepository.GetByDomainNameAsync(domain, cancellationToken);
        if (tenant is null)
        {
            await WriteAuditAsync(null, null, LoginFailedAction,
                $"Tenant no encontrado para dominio: {domain}", cancellationToken);
            logger.LogWarning("HRD: dominio '{Domain}' no encontrado", domain);
            return Error.TenantNotFound;
        }

        // 4 — Resolver configuración del proveedor
        var authConfig = tenant.AuthConfigs
            .FirstOrDefault(c => c.ProviderType == provider && c.IsActive);
        if (authConfig is null)
        {
            await WriteAuditAsync(tenant.Id, null, LoginFailedAction,
                $"AuthConfig no encontrada para proveedor {provider}", cancellationToken);
            return Error.AuthConfigNotFound;
        }

        // 5 — Validar firma del token contra el JWKS oficial del proveedor
        var validationResult = await externalTokenValidator.ValidateAsync(
            request.IdToken,
            provider,
            authConfig.IssuerUrl ?? rawIssuer,
            authConfig.ClientId,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            await WriteAuditAsync(tenant.Id, null, LoginFailedAction,
                "Token externo inválido", cancellationToken);
            return validationResult.Error;
        }

        var claims = validationResult.Value;

        // 6 — Buscar usuario por ExternalId (NO hay JIT provisioning)
        var user = await userRepository.GetByExternalIdAsync(claims.ExternalId, cancellationToken);
        if (user is null)
        {
            await WriteAuditAsync(tenant.Id, null, LoginFailedAction,
                $"Usuario no provisionado: {email}", cancellationToken);
            logger.LogWarning(
                "Acceso rechazado: usuario '{Email}' no provisionado en tenant {TenantId}",
                email, tenant.Id);
            return Error.UserNotProvisioned;
        }

        // 7 — Verificar que el usuario pertenece a este tenant
        if (user.TenantId != tenant.Id)
        {
            await WriteAuditAsync(tenant.Id, user.Id, LoginFailedAction,
                "Tenant mismatch", cancellationToken);
            return Error.UserNotProvisioned;
        }

        // 8 — Verificar que el usuario esté activo
        if (user.Status?.Code == UserInactiveCode)
        {
            await WriteAuditAsync(tenant.Id, user.Id, LoginFailedAction,
                "Usuario inactivo", cancellationToken);
            return Error.UserInactive;
        }

        // 9 — Obtener permisos efectivos
        var permissions = await userRepository.GetPermissionsAsync(user.Id, cancellationToken);

        // 10 — Registrar último login
        user.RecordLogin();
        await userRepository.UpdateAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        // 11 — Emitir JWT interno del SaaS
        var tokenResult = await tokenService.GenerateInternalTokenAsync(
            user.Id, tenant.Id, permissions, cancellationToken);

        // 12 — Audit log de login exitoso
        await WriteAuditAsync(
            tenant.Id, user.Id, LoginAction,
            "Login exitoso", cancellationToken,
            entityId: user.Id);

        logger.LogInformation(
            "Login exitoso: usuario {UserId} en tenant {TenantId}",
            user.Id, tenant.Id);

        return new ExchangeTokenResponseDto(tokenResult.AccessToken, tokenResult.ExpiresIn);
    }

    // ── Helpers privados ────────────────────────────────────────────────────

    private static (string Email, string Issuer)? PreDecodeJwtPayload(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            // Base64Url → Base64 estándar
            var payload = parts[1];
            var padding = (4 - payload.Length % 4) % 4;
            payload += new string('=', padding);
            payload = payload.Replace('-', '+').Replace('_', '/');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Entra ID usa 'preferred_username' cuando el claim 'email' no está presente
            var email = root.TryGetProperty("email", out var ep) ? ep.GetString() : null;
            email ??= root.TryGetProperty("preferred_username", out var pp) ? pp.GetString() : null;
            email ??= root.TryGetProperty("upn", out var up) ? up.GetString() : null;

            var issuer = root.TryGetProperty("iss", out var ip) ? ip.GetString() : null;

            return email is not null && issuer is not null ? (email, issuer) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..].ToLowerInvariant() : string.Empty;
    }

    private async Task WriteAuditAsync(
        Guid? tenantId,
        Guid? userId,
        string action,
        string detail,
        CancellationToken cancellationToken,
        Guid? entityId = null)
    {
        var payload = JsonSerializer.Serialize(new { detail });
        var log = AuditLog.Create(
            tenantId, userId, action, UserEntity,
            entityId: entityId,
            newValues: JsonDocument.Parse(payload));

        await auditLogRepository.AddAsync(log, cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);
    }
}
