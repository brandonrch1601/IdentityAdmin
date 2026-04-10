namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Catálogo de estados del sistema (Cat_Statuses).
/// Agrupa estados por entidad: TENANT, USER, CUSTOMER, DOCUMENT.
/// </summary>
public sealed class CatStatus
{
    private CatStatus() { } // EF Core

    public int Id { get; private set; }

    /// <summary>Agrupación del estado: 'TENANT', 'USER', 'CUSTOMER', 'DOCUMENT'.</summary>
    public string GroupName { get; private set; } = default!;

    /// <summary>Código único dentro del grupo: 'ACTIVE', 'INACTIVE', etc.</summary>
    public string Code { get; private set; } = default!;

    public string Description { get; private set; } = default!;
}
