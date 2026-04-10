using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Infrastructure.Persistence.Configurations;

/// <summary>Configuración EF de la tabla Cat_Statuses con datos semilla.</summary>
internal sealed class CatStatusConfiguration : IEntityTypeConfiguration<CatStatus>
{
    public void Configure(EntityTypeBuilder<CatStatus> builder)
    {
        builder.ToTable("cat_statuses");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(s => s.GroupName).HasColumnName("group_name").HasMaxLength(50).IsRequired();
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.GroupName, s.Code }).IsUnique();

        // Datos semilla alineados con el INSERT de sql
        builder.HasData(
            new { Id = 1, GroupName = "TENANT", Code = "ACTIVE", Description = "Activo" },
            new { Id = 2, GroupName = "TENANT", Code = "SUSPENDED", Description = "Suspendido" },
            new { Id = 3, GroupName = "USER", Code = "ACTIVE", Description = "Activo" },
            new { Id = 4, GroupName = "USER", Code = "INACTIVE", Description = "Inactivo" },
            new { Id = 5, GroupName = "CUSTOMER", Code = "ACTIVE", Description = "Activo" },
            new { Id = 6, GroupName = "CUSTOMER", Code = "INACTIVE", Description = "Inactivo" },
            new { Id = 7, GroupName = "DOCUMENT", Code = "PENDING", Description = "Pendiente de Revisión" },
            new { Id = 8, GroupName = "DOCUMENT", Code = "VALID", Description = "Válido" },
            new { Id = 9, GroupName = "DOCUMENT", Code = "EXPIRED", Description = "Vencido" },
            new { Id = 10, GroupName = "DOCUMENT", Code = "REJECTED", Description = "Rechazado" }
        );
    }
}

/// <summary>Configuración EF de la tabla Audit_Logs.</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityName).HasColumnName("entity_name").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("NOW()");

        // Índice compuesto para consultas de auditoría por tenant
        builder.HasIndex(a => new { a.TenantId, a.Timestamp });
    }
}

/// <summary>Configuración EF de la tabla Permissions con datos semilla.</summary>
internal sealed class PermissionSeedConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // Los datos semilla de Permissions se configuran aquí para mantenerlos
        // junto al seed de Cat_Statuses
        builder.HasData(
            new { Id = 1, Code = "DOC_VIEW", Description = "Ver documentos" },
            new { Id = 2, Code = "DOC_UPLOAD", Description = "Subir documentos" },
            new { Id = 3, Code = "EXP_APPROVE", Description = "Aprobar expedientes" },
            new { Id = 4, Code = "USER_ADMIN", Description = "Administrar usuarios" }
        );
    }
}
