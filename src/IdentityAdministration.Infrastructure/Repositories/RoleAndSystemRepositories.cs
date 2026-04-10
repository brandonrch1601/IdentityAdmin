using Microsoft.EntityFrameworkCore;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Infrastructure.Persistence;

namespace IdentityAdministration.Infrastructure.Repositories;

internal sealed class RoleRepository(IdentityDbContext context) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> AllBelongToTenantAsync(
        Guid tenantId,
        IEnumerable<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = roleIds.ToList();
        var count = await context.Roles
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId && ids.Contains(r.Id), cancellationToken);
        return count == ids.Count;
    }

    public async Task<bool> ExistsByNameAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default) =>
        await context.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == name, cancellationToken);

    public async Task AddAsync(Role role, CancellationToken cancellationToken = default) =>
        await context.Roles.AddAsync(role, cancellationToken);

    public async Task ReplacePermissionsAsync(
        int roleId,
        IEnumerable<int> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        context.RolePermissions.RemoveRange(existing);

        var newPerms = permissionIds.Select(pid => RolePermission.Create(roleId, pid));
        await context.RolePermissions.AddRangeAsync(newPerms, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}

internal sealed class AuditLogRepository(IdentityDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        await context.AuditLogs.AddAsync(auditLog, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}

internal sealed class PermissionRepository(IdentityDbContext context) : IPermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await context.Permissions
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

    public async Task<bool> AllExistAsync(
        IEnumerable<int> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = permissionIds.ToList();
        var count = await context.Permissions
            .CountAsync(p => ids.Contains(p.Id), cancellationToken);
        return count == ids.Count;
    }
}
