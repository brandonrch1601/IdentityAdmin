using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Domain.Interfaces.Repositories;

/// <summary>
/// Puerto de repositorio para AuditLog.
/// El registro de auditoría es de solo escritura (append-only).
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
