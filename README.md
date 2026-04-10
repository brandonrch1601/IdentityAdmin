# Identity Administration

Microservicio de administración de identidad multi-tenant. Implementa el dominio de servicio **Identity Administration** de BIAN y actúa como **Authorization Server** interno para todos los microservicios de la plataforma.

## Responsabilidades

- **Home Realm Discovery (HRD):** identifica el proveedor de identidad (Entra ID / Google) a partir del dominio del correo corporativo del usuario.
- **Token Exchange:** valida tokens OIDC externos, verifica que el usuario esté provisionado y emite un **JWT interno** firmado con RSA 4096 que incluye permisos RBAC.
- **JWKS Endpoint:** expone las claves públicas RSA para que los demás microservicios validen los tokens sin depender de este servicio en cada request.
- **Administración de usuarios:** provisioning, asignación de roles y control de estado (activo/inactivo).
- **Administración de roles y permisos:** CRUD de roles con asignación de permisos granulares por tenant.

> El servicio **no almacena contraseñas**. La autenticación es completamente delegada a los proveedores de identidad externos. No existe auto-registro (JIT provisioning) — solo los administradores del tenant pueden provisionar usuarios.

---

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| Framework | ASP.NET Core 10 |
| ORM | Entity Framework Core 10 + Npgsql |
| Base de datos | PostgreSQL 15+ |
| Firm de tokens | Azure Key Vault (RSA 4096) / RSA local en desarrollo |
| Autenticación externa | Microsoft Entra ID / Google (OIDC) |
| Arquitectura | Clean Architecture + CQRS (MediatR) |
| Validación | FluentValidation |

---

## Estructura del proyecto

```
IdentityAdministration/
├── src/
│   ├── IdentityAdministration.Domain/         # Entidades, interfaces, value objects
│   ├── IdentityAdministration.Application/    # Comandos, queries, behaviors (CQRS)
│   ├── IdentityAdministration.Infrastructure/ # EF Core, repositorios, servicios externos
│   └── IdentityAdministration.API/            # Controllers, middleware, Program.cs

```

---

## Requisitos previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 15+ (local o Azure Database for PostgreSQL)
- Acceso a Azure Key Vault con una clave RSA 4096 *(solo en producción)*
- App Registration en Microsoft Entra ID *(para autenticación con cuentas Microsoft)*

---

## Configuración

### Variables de entorno / appsettings

Toda la configuración sensible debe sobrescribirse en `appsettings.Development.json` (local) o mediante variables de entorno / secrets en AKS.

```jsonc
// appsettings.json — valores de producción de referencia
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>"
  },
  "AzureKeyVault": {
    "VaultUri": "https://<vault-name>.vault.azure.net/",
    "TokenSigningKeyName": "token-signing"   // Nombre de la clave RSA en AKV
  },
  "Jwt": {
    "Issuer": "https://identity.tecasoft.cr", // URL pública del servicio
    "Audience": "services",                   // Debe coincidir en todos los consumers
    "ExpirationMinutes": 60
  }
}
```

| Variable | Descripción | Requerida en |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión PostgreSQL | Dev + Prod |
| `AzureKeyVault:VaultUri` | URI del Key Vault | Solo Prod |
| `AzureKeyVault:TokenSigningKeyName` | Nombre de la clave RSA en AKV | Solo Prod |
| `Jwt:Issuer` | Claim `iss` del JWT emitido | Dev + Prod |
| `Jwt:Audience` | Claim `aud` del JWT emitido | Dev + Prod |
| `Jwt:ExpirationMinutes` | Duración del token interno | Dev + Prod |

### Desarrollo local

En `appsettings.Development.json`, dejar `AzureKeyVault:VaultUri` vacío. El servicio detecta el entorno `Development` y usa `LocalRsaTokenService`, que genera automáticamente un par de claves RSA en memoria al arrancar.

```jsonc
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sged_dev;Username=postgres;Password=<password>"
  },
  "AzureKeyVault": {
    "VaultUri": "",                          // Vacío → usa RSA local
    "TokenSigningKeyName": "sged-token-signing"
  },
  "Jwt": {
    "Issuer": "https://localhost:7001",
    "Audience": "sged-services",
    "ExpirationMinutes": 480
  }
}
```

---

## Base de datos

### Crear el schema

```bash
psql -h <host> -U <user> -d <database> -f docs/sql
```

### Insertar datos de prueba

```bash
psql -h <host> -U <user> -d <database> -f docs/seed_test_data.sql
```

El seed crea:
- **2 tenants** con su configuración de autenticación Microsoft
- **6 roles** con permisos asignados
- **8 usuarios** de ejemplo

> ⚠️ Tras ejecutar el seed, actualiza los registros de `tenant_auth_configs` con el `client_id` e `issuer_url` reales de tu App Registration de Entra ID, y actualiza `users.external_id` con el `oid` real de cada usuario.

```sql
-- Actualizar con valores reales del App Registration
UPDATE tenant_auth_configs
SET client_id  = '<application-client-id>',
    issuer_url = 'https://login.microsoftonline.com/<tenant-id>/v2.0'
WHERE tenant_id = '<tu-tenant-id>';

-- Actualizar con el OID real del usuario en Entra ID
UPDATE users
SET external_id = '<oid-del-usuario>'
WHERE email = '<email>';
```

---

## Ejecutar localmente

```bash
cd src/IdentityAdministration.API
dotnet run
```

El API estará disponible en:
- **HTTPS:** `https://localhost:7001`
- **HTTP:** `http://localhost:5001`
- **Swagger UI:** `https://localhost:7001/swagger`

---

## Flujo de autenticación

### 1. Home Realm Discovery

Identifica el proveedor de identidad a partir del dominio del correo.

```http
GET /auth/discovery/usuario@empresa.com
```

**Respuesta:**
```json
{
  "provider": "Microsoft",
  "tenantName": "Banco Nacional",
  "authUrl": "https://login.microsoftonline.com/..."
}
```

### 2. Obtener ID Token externo (Postman / cliente)

Configura OAuth 2.0 en Postman con los siguientes parámetros:

| Campo | Valor |
|---|---|
| Grant Type | Authorization Code (with PKCE) |
| Auth URL | `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/authorize` |
| Access Token URL | `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token` |
| Client ID | `<application-client-id>` |
| Client Secret | `<client-secret>` *(creado en el App Registration)* |
| Scope | `openid email profile` |
| Client Authentication | Send client credentials in body |

> El token que debes usar en el siguiente paso es el **`id_token`**, no el `access_token`. Revisa el JSON completo del response de Postman para encontrarlo.

### 3. Token Exchange

Envía el ID Token externo para obtener el JWT interno del SaaS.

```http
POST /auth/exchange
Content-Type: application/json

{
  "idToken": "<id_token_de_microsoft_o_google>",
  "provider": "Microsoft"
}
```

**Respuesta exitosa:**
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600
}
```

El `accessToken` es el JWT interno que debes incluir en el header `Authorization: Bearer <token>` para todas las llamadas a este microservicio y a los demás microservicios de 

---

## Uso del JWT en otros microservicios

El token emitido contiene los siguientes claims:

| Claim | Descripción |
|---|---|
| `sub` | ID interno del usuario (UUID) |
| `tenant_id` | ID del tenant al que pertenece el usuario |
| `permissions` | Array de strings con los permisos RBAC del usuario |
| `iss` | Emisor (`Jwt:Issuer` configurado) |
| `aud` | Audiencia (`sged-services`) |

Los demás microservicios validan este token descargando las claves públicas desde:

```
GET /.well-known/jwks.json
```

Configuración mínima en un consumer ASP.NET Core:

```json
{
  "Jwt": {
    "Authority": "https://identity.tecasoft.cr",
    "Audience": "sged-services"
  }
}
```

---

## Endpoints principales

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/.well-known/jwks.json` | No | Claves públicas RSA para validación de JWT |
| `GET` | `/auth/discovery/{email}` | No | Home Realm Discovery |
| `POST` | `/auth/exchange` | No | Token Exchange — emite JWT interno |
| `GET` | `/health` | No | Health check |
| `GET` | `/users` | Bearer | Lista usuarios del tenant (paginado) |
| `POST` | `/users` | Bearer | Provisiona un nuevo usuario |
| `GET` | `/users/{id}` | Bearer | Detalle de usuario |
| `PUT` | `/users/{id}/roles` | Bearer | Asigna roles al usuario |
| `PATCH` | `/users/{id}/status` | Bearer | Activa o desactiva un usuario |
| `GET` | `/roles` | Bearer | Lista roles del tenant con sus permisos |
| `PUT` | `/roles/{id}/permissions` | Bearer | Asigna permisos a un rol |
| `GET` | `/permissions` | Bearer | Lista todos los permisos del sistema |

La especificación OpenAPI completa está disponible en [`docs/Identity.Administration.openapi.json`](docs/Identity.Administration.openapi.json) e importable directamente en Postman o [editor.swagger.io](https://editor.swagger.io).

---

## Configuración de Azure App Registration (Entra ID)

Para habilitar la autenticación con cuentas de Microsoft:

1. Crear un **App Registration** en Azure Portal → Entra ID.
2. En **Authentication → Add a platform**, agregar tipo **Web** con las URIs:
   - `https://oauth.pstmn.io/v1/callback` *(Postman)*
   - `https://oauth.pstmn.io/v1/browser-callback` *(Postman con browser)*
3. En **Certificates & secrets**, crear un client secret para desarrollo.
4. En **API permissions**, asegurarse de incluir: `openid`, `email`, `profile`.
5. Copiar el **Application (client) ID** y el **Directory (tenant) ID**.
6. Actualizar la tabla `tenant_auth_configs` con estos valores (ver sección Base de datos).

---

## Configuración de Azure Key Vault (Producción)

El firmado de JWT en producción utiliza una clave RSA 4096 almacenada en Azure Key Vault. El pod de AKS accede al vault mediante **Workload Identity** (OIDC Federated Credential), sin necesidad de almacenar credenciales.

### Crear la clave

```bash
az keyvault key create \
  --vault-name sged-vault \
  --name sged-token-signing \
  --kty RSA \
  --size 4096 \
  --ops sign verify
```

### Permisos requeridos

La Managed Identity del pod AKS necesita el rol **Key Vault Crypto User** sobre la clave:

```bash
az role assignment create \
  --role "Key Vault Crypto User" \
  --assignee <managed-identity-principal-id> \
  --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/sged-vault
```

---

## Consideraciones de seguridad

- **No JIT provisioning:** los usuarios deben ser provisionados explícitamente por un administrador del tenant antes de poder autenticarse.
- **Multi-tenancy estricto:** todos los queries aplican Global Query Filters por `tenant_id`. Los repositorios que se llaman antes de tener un JWT activo usan `IgnoreQueryFilters()` de forma explícita y documentada.
- **Audit Log:** todas las operaciones sensibles (logins, cambios de estado, asignación de roles) se registran en la tabla `audit_logs` de forma append-only.
- **RBAC granular:** la autorización se implementa con el atributo `[HasPermission("CODIGO")]` que resuelve dinámicamente las políticas en tiempo de ejecución.
