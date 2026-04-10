using Microsoft.EntityFrameworkCore;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Infrastructure.Persistence;

namespace IdentityAdministration.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Include(u => u.Status)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Include(u => u.Status)
            .IgnoreQueryFilters() // Pre-login: TenantContext no está inicializado aún
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

    public async Task<User?> GetByEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.TenantId == tenantId && u.Email == email.ToLowerInvariant(),
                cancellationToken);

    public async Task<bool> ExistsByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.ExternalId == externalId, cancellationToken);

    public async Task<bool> ExistsByEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.TenantId == tenantId && u.Email == email.ToLowerInvariant(),
                cancellationToken);

    public async Task<PagedResult<User>> GetPagedAsync(
        Guid tenantId,
        string? statusCode,
        string? email,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users
            .AsNoTracking()
            .Include(u => u.Status)
            .Include(u => u.UserRoles)
            .Where(u => u.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(statusCode))
            query = query.Where(u => u.Status != null && u.Status.Code == statusCode.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email.Contains(email.ToLowerInvariant()));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.UserRoles
            .IgnoreQueryFilters() // Llamado durante Token Exchange: TenantContext aún no inicializado.
                                  // userId es UUID único globalmente → no hay fuga entre tenants.
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await context.Users.AddAsync(user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task ReplaceRolesAsync(
        Guid userId,
        IEnumerable<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        context.UserRoles.RemoveRange(existing);

        var newRoles = roleIds.Select(rid => UserRole.Create(userId, rid));
        await context.UserRoles.AddRangeAsync(newRoles, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
