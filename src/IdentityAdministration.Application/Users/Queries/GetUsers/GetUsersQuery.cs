using FluentValidation;
using MediatR;
using IdentityAdministration.Application.Users.Commands.CreateUser;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Users.Queries.GetUsers;

// ── Query ────────────────────────────────────────────────────────────────────

/// <summary>
/// Lista paginada de usuarios del tenant autenticado.
/// El Global Query Filter de EF Core garantiza el aislamiento.
/// </summary>
public record GetUsersQuery(
    string? StatusCode = null,
    string? Email = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<UserDto>>>;

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class GetUsersQueryHandler(
    ITenantContext tenantContext,
    IUserRepository userRepository)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<UserDto>>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var pagedUsers = await userRepository.GetPagedAsync(
            tenantId,
            request.StatusCode,
            request.Email,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = pagedUsers.Items
            .Select(u => new UserDto(
                u.Id, u.TenantId, u.Email, u.FullName, u.ExternalId,
                u.Status?.Code ?? string.Empty,
                u.LastLogin, u.CreatedAt, u.LicenseSeat,
                u.UserRoles.Select(ur => ur.RoleId).ToList()))
            .ToList();

        return Result<PagedResult<UserDto>>.Success(
            new PagedResult<UserDto>(dtos, pagedUsers.TotalCount, request.Page, request.PageSize));
    }
}

// ── Validator ────────────────────────────────────────────────────────────────

internal sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("La página debe ser mayor a 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El filtro de email no tiene formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
