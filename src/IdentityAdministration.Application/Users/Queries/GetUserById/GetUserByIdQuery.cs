using MediatR;
using Microsoft.Extensions.Logging;
using IdentityAdministration.Application.Users.Commands.CreateUser;
using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Domain.Interfaces.Services;

namespace IdentityAdministration.Application.Users.Queries.GetUserById;

// ── Query ────────────────────────────────────────────────────────────────────

public record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserDto>>;

// ── Handler ──────────────────────────────────────────────────────────────────

internal sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return Error.UserNotFound;

        return new UserDto(
            user.Id, user.TenantId, user.Email, user.FullName, user.ExternalId,
            user.Status?.Code ?? string.Empty,
            user.LastLogin, user.CreatedAt, user.LicenseSeat,
            user.UserRoles.Select(ur => ur.RoleId).ToList());
    }
}
