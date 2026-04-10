using Microsoft.AspNetCore.Authorization;

namespace IdentityAdministration.API.Authorization;

// ── Requirement ───────────────────────────────────────────────────────────────

/// <summary>
/// Requerimiento de autorización que verifica un permiso específico
/// en el claim <c>permissions</c> del JWT interno del SaaS.
/// </summary>
public sealed class HasPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Evalúa si el usuario autenticado posee el permiso requerido en sus claims.
/// Permite decorar controllers con <c>[HasPermission("DOC_VIEW")]</c>.
/// </summary>
public sealed class HasPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    private const string PermissionsClaim = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        // El claim 'permissions' puede ser un array JSON o múltiples claims
        var permissionClaims = context.User.Claims
            .Where(c => c.Type == PermissionsClaim)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permissionClaims.Contains(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

// ── Attribute ─────────────────────────────────────────────────────────────────

/// <summary>
/// Atributo de autorización que verifica el permiso indicado en el JWT interno.
/// Puede aplicarse a controllers o action methods.
/// Ejemplo: <c>[HasPermission("USER_ADMIN")]</c>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(policy: $"Permission:{permission}")
{
    public string Permission { get; } = permission;
}

// ── Policy Provider ───────────────────────────────────────────────────────────

/// <summary>
/// Proveedor dinámico de policies que construye una política de autorización
/// por cada permiso registrado con el prefijo <c>Permission:</c>.
/// Permite usar <c>[HasPermission("X")]</c> sin registrar cada permiso manualmente.
/// </summary>
public sealed class HasPermissionPolicyProvider(IAuthorizationPolicyProvider fallback)
    : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "Permission:";

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new HasPermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        fallback.GetFallbackPolicyAsync();
}
