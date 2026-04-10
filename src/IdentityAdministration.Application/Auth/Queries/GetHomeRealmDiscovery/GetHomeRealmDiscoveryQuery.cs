using FluentValidation;
using MediatR;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Interfaces.Repositories;

namespace IdentityAdministration.Application.Auth.Queries.GetHomeRealmDiscovery;

// ── Query ────────────────────────────────────────────────────────────────────

/// <summary>
/// Home Realm Discovery: a partir del email del usuario, identifica el tenant
/// y retorna la configuración del proveedor de identidad al que debe redirigir el frontend.
/// </summary>
public record GetHomeRealmDiscoveryQuery(string Email)
    : IRequest<Result<HomeRealmDiscoveryDto>>;

// ── DTO de respuesta ─────────────────────────────────────────────────────────

public record HomeRealmDiscoveryDto(
    Guid TenantId,
    string TenantName,
    /// <summary>GOOGLE o MICROSOFT.</summary>
    string ProviderType,
    string ClientId,
    string IssuerUrl);

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class GetHomeRealmDiscoveryQueryHandler(ITenantRepository tenantRepository)
    : IRequestHandler<GetHomeRealmDiscoveryQuery, Result<HomeRealmDiscoveryDto>>
{
    public async Task<Result<HomeRealmDiscoveryDto>> Handle(
        GetHomeRealmDiscoveryQuery request,
        CancellationToken cancellationToken)
    {
        var domain = ExtractDomain(request.Email);
        var tenant = await tenantRepository.GetByDomainNameAsync(domain, cancellationToken);

        if (tenant is null)
            return Error.TenantNotFound;

        var activeConfig = tenant.AuthConfigs.FirstOrDefault(c => c.IsActive);
        if (activeConfig is null)
            return Error.AuthConfigNotFound;

        return new HomeRealmDiscoveryDto(
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            ProviderType: activeConfig.ProviderType.ToString().ToUpperInvariant(),
            ClientId: activeConfig.ClientId,
            IssuerUrl: activeConfig.IssuerUrl ?? string.Empty);
    }

    private static string ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0
            ? email[(atIndex + 1)..].ToLowerInvariant()
            : email.ToLowerInvariant();
    }
}

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class GetHomeRealmDiscoveryQueryValidator : AbstractValidator<GetHomeRealmDiscoveryQuery>
{
    public GetHomeRealmDiscoveryQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(255).WithMessage("El correo no puede exceder 255 caracteres.");
    }
}
