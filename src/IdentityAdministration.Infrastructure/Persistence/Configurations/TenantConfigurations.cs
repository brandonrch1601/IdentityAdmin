using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Infrastructure.Persistence.Configurations;

/// <summary>Configuración EF de la tabla Tenants.</summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(t => t.IdentificationNumber).HasColumnName("identification_number").HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.IdentificationNumber).IsUnique();
        builder.Property(t => t.DomainName).HasColumnName("domain_name").HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.DomainName).IsUnique();
        builder.Property(t => t.StatusId).HasColumnName("status_id");
        builder.Property(t => t.BrandingConfig).HasColumnName("branding_config").HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(t => t.Status)
            .WithMany()
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.AuthConfigs)
            .WithOne(c => c.Tenant)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.AuthConfigs)
            .HasField("_authConfigs")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(t => t.Users)
            .HasField("_users")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(t => t.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Configuración EF de la tabla Tenant_Auth_Configs.</summary>
internal sealed class TenantAuthConfigConfiguration : IEntityTypeConfiguration<TenantAuthConfig>
{
    public void Configure(EntityTypeBuilder<TenantAuthConfig> builder)
    {
        builder.ToTable("tenant_auth_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.ProviderType).HasColumnName("provider_type").HasMaxLength(20).IsRequired()
            .HasConversion<string>();
        builder.Property(c => c.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(c => c.IssuerUrl).HasColumnName("issuer_url");
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasIndex(c => new { c.TenantId, c.ProviderType }).IsUnique();
    }
}
