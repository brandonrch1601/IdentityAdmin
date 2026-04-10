namespace IdentityAdministration.Domain.Common;

/// <summary>
/// Representa un error de dominio con código y mensaje estandarizados.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error Unknown = new("UNKNOWN_ERROR", "Ha ocurrido un error desconocido.");

    // Auth
    public static readonly Error UserNotProvisioned = new(
        "USER_NOT_PROVISIONED",
        "El usuario no ha sido provisionado. Contacte a su administrador.");
    public static readonly Error UserInactive = new(
        "USER_INACTIVE",
        "La cuenta del usuario se encuentra inactiva.");
    public static readonly Error InvalidExternalToken = new(
        "INVALID_EXTERNAL_TOKEN",
        "El token del proveedor de identidad es inválido o ha expirado.");
    public static readonly Error TenantNotFound = new(
        "TENANT_NOT_FOUND",
        "No se encontró ningún tenant asociado al dominio indicado.");
    public static readonly Error AuthConfigNotFound = new(
        "AUTH_CONFIG_NOT_FOUND",
        "El tenant no tiene configurado el proveedor de identidad especificado.");

    // Users
    public static readonly Error UserAlreadyExists = new(
        "USER_ALREADY_EXISTS",
        "Ya existe un usuario con ese correo electrónico o ID externo en este tenant.");
    public static readonly Error UserNotFound = new(
        "USER_NOT_FOUND",
        "El usuario no fue encontrado.");
    public static readonly Error EmailDomainMismatch = new(
        "EMAIL_DOMAIN_MISMATCH",
        "El dominio del correo electrónico no corresponde al tenant.");

    // Roles
    public static readonly Error RoleNotFound = new(
        "ROLE_NOT_FOUND",
        "El rol indicado no fue encontrado o no pertenece a este tenant.");
    public static readonly Error RoleAlreadyExists = new(
        "ROLE_ALREADY_EXISTS",
        "Ya existe un rol con ese nombre en este tenant.");
    public static readonly Error InvalidRoleIds = new(
        "INVALID_ROLE_IDS",
        "Uno o más IDs de rol son inválidos o no pertenecen a este tenant.");
    public static readonly Error InvalidPermissionIds = new(
        "INVALID_PERMISSION_IDS",
        "Uno o más IDs de permiso son inválidos.");
}

/// <summary>
/// Resultado de una operación sin valor de retorno.
/// </summary>
public sealed class Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Resultado de una operación con valor de retorno de tipo <typeparamref name="T"/>.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = Error.None;
    }

    private Result(Error error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

/// <summary>
/// Resultado paginado para queries de listado.
/// </summary>
/// <typeparam name="T">Tipo de cada item.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
