using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Enums;
using IdentityAdministration.Domain.Interfaces.Services;
using IdentityAdministration.Infrastructure.Persistence.Configurations;

namespace IdentityAdministration.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core para el dominio de Identity Administration.
/// Aplica Global Query Filters por TenantId en todas las entidades que lo soporten.
/// Cuando <see cref="ITenantContext.IsAuthenticated"/> es false (flujo de login),
/// los filtros no se aplican, permitiendo búsquedas pre-autenticación.
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // ── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantAuthConfig> TenantAuthConfigs => Set<TenantAuthConfig>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<CatStatus> CatStatuses => Set<CatStatus>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas las configuraciones de la carpeta Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // ── Global Query Filters (aislamiento multi-tenant) ─────────────────
        // Cuando IsAuthenticated=false (login flow), no se filtra por tenant.
        // Cuando IsAuthenticated=true, solo se ven datos del tenant autenticado.
        modelBuilder.Entity<User>()
            .HasQueryFilter(u =>
                !_tenantContext.IsAuthenticated ||
                u.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<Role>()
            .HasQueryFilter(r =>
                !_tenantContext.IsAuthenticated ||
                r.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<TenantAuthConfig>()
            .HasQueryFilter(c =>
                !_tenantContext.IsAuthenticated ||
                c.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(a =>
                !_tenantContext.IsAuthenticated ||
                a.TenantId == _tenantContext.TenantId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Almacenar enums como strings en PostgreSQL
        configurationBuilder
            .Properties<ProviderType>()
            .HaveConversion<string>()
            .HaveMaxLength(20);
    }
}
