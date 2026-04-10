using FluentValidation;
using MediatR;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Enums;

namespace IdentityAdministration.Application.Auth.Commands.AuthenticateExternalUser;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Paso 2 del flujo de autenticación: recibe el ID Token del proveedor externo,
/// lo valida, verifica que el usuario esté provisionado y emite un JWT interno del SaaS.
/// No realiza JIT provisioning; el usuario debe haber sido creado previamente por un admin.
/// </summary>
public record AuthenticateExternalUserCommand(
    /// <summary>ID Token emitido por Google o Microsoft Entra ID.</summary>
    string IdToken,
    /// <summary>Proveedor: "GOOGLE" o "MICROSOFT".</summary>
    string Provider)
    : IRequest<Result<ExchangeTokenResponseDto>>;

// ── DTO de respuesta ─────────────────────────────────────────────────────────

public record ExchangeTokenResponseDto(
    string AccessToken,
    int ExpiresIn,
    string TokenType = "Bearer");

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class AuthenticateExternalUserCommandValidator
    : AbstractValidator<AuthenticateExternalUserCommand>
{
    private static readonly string[] AllowedProviders = ["GOOGLE", "MICROSOFT"];

    public AuthenticateExternalUserCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("El ID Token es requerido.")
            .Must(t => t.Split('.').Length == 3)
            .WithMessage("El formato del ID Token JWT no es válido.");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("El proveedor de identidad es requerido.")
            .Must(p => AllowedProviders.Contains(p.ToUpperInvariant()))
            .WithMessage("El proveedor debe ser GOOGLE o MICROSOFT.");
    }
}
