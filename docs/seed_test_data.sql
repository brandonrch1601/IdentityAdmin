-- =============================================================================
-- SGED Identity Administration — Datos de Prueba
-- =============================================================================
-- Propósito  : Poblar la base de datos con datos realistas para desarrollo y QA.
-- Entorno    : Desarrollo / Staging  (NO ejecutar en PRODUCCIÓN)
-- Compatibil.: PostgreSQL 15+
-- Ejecutar   : psql -U postgres -d sged -f seed_test_data.sql
--
-- Escenario cubierto:
--   • 2 tenants bancarios (Banco Meridian CR, Financiera Andes SA)
--   • Cada tenant usa un IdP diferente (Microsoft y Google)
--   • 3 roles por tenant con permisos RBAC diferenciados
--   • 4 usuarios por tenant (admin, analista, revisor, inactivo)
--   • Registros de auditoría de ejemplo
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 0. PRECAUCIÓN: Limpiar datos de prueba previos (orden FK inversas)
-- -----------------------------------------------------------------------------
DELETE FROM Audit_Logs    WHERE tenant_id IN (
    SELECT id FROM Tenants WHERE identification_number IN ('3-101-123456','3-102-654321'));
DELETE FROM User_Roles    WHERE user_id IN (
    SELECT id FROM Users WHERE email LIKE '%meridian.cr' OR email LIKE '%andesfinanciera.cr');
DELETE FROM Role_Permissions WHERE role_id IN (
    SELECT id FROM Roles WHERE tenant_id IN (
        SELECT id FROM Tenants WHERE identification_number IN ('3-101-123456','3-102-654321')));
DELETE FROM Users         WHERE email LIKE '%meridian.cr' OR email LIKE '%andesfinanciera.cr';
DELETE FROM Roles         WHERE tenant_id IN (
    SELECT id FROM Tenants WHERE identification_number IN ('3-101-123456','3-102-654321'));
DELETE FROM Tenant_Auth_Configs WHERE tenant_id IN (
    SELECT id FROM Tenants WHERE identification_number IN ('3-101-123456','3-102-654321'));
DELETE FROM Tenants       WHERE identification_number IN ('3-101-123456','3-102-654321');

-- =============================================================================
-- 1. TENANTS
-- =============================================================================

-- Tenant A — Banco Meridian CR (usa Microsoft Entra ID)
INSERT INTO Tenants (id, name, identification_number, domain_name, status_id, branding_config)
VALUES (
    'a1b2c3d4-0001-4000-8000-000000000001',
    'Banco Meridian CR',
    '3-101-123456',
    'meridian.cr',
    (SELECT id FROM Cat_Statuses WHERE group_name = 'TENANT' AND code = 'ACTIVE'),
    '{"primaryColor":"#003366","logoUrl":"https://assets.meridian.cr/logo.svg","appName":"SGED Meridian"}'
);

-- Tenant B — Financiera Andes SA (usa Google Workspace)
INSERT INTO Tenants (id, name, identification_number, domain_name, status_id, branding_config)
VALUES (
    'a1b2c3d4-0002-4000-8000-000000000002',
    'Financiera Andes SA',
    '3-102-654321',
    'andesfinanciera.cr',
    (SELECT id FROM Cat_Statuses WHERE group_name = 'TENANT' AND code = 'ACTIVE'),
    '{"primaryColor":"#8B0000","logoUrl":"https://assets.andesfinanciera.cr/logo.svg","appName":"SGED Andes"}'
);

-- =============================================================================
-- 2. CONFIGURACIONES DE AUTENTICACIÓN (IdP por tenant)
-- =============================================================================

-- Tenant A — Microsoft Entra ID
-- client_id y issuer_url simulados para pruebas locales
INSERT INTO Tenant_Auth_Configs (id, tenant_id, provider_type, client_id, issuer_url, is_active)
VALUES (
    'b1000000-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'Microsoft',
    'aaaaaaaa-1111-4000-8000-aaaaaaaaaaaa',  -- client_id del App Registration en Entra ID
    'https://login.microsoftonline.com/bbbbbbbb-2222-4000-8000-bbbbbbbbbbbb/v2.0',
    TRUE
);

-- Tenant B — Google Workspace
INSERT INTO Tenant_Auth_Configs (id, tenant_id, provider_type, client_id, issuer_url, is_active)
VALUES (
    'b2000000-0002-4000-8000-000000000002',
    'a1b2c3d4-0002-4000-8000-000000000002',
    'Google',
    '123456789-abcdefghijklmnop.apps.googleusercontent.com',  -- client_id de GCP Console
    'https://accounts.google.com',
    TRUE
);

-- =============================================================================
-- 3. ROLES Y PERMISOS
-- =============================================================================
-- Lectura rápida de IDs de permisos del catálogo
-- DOC_VIEW=1, DOC_UPLOAD=2, EXP_APPROVE=3, USER_ADMIN=4

-- ── TENANT A — Banco Meridian CR ──────────────────────────────────────────────

INSERT INTO Roles (tenant_id, name, description) VALUES
('a1b2c3d4-0001-4000-8000-000000000001', 'Administrador',  'Gestión completa de usuarios y configuración del tenant'),
('a1b2c3d4-0001-4000-8000-000000000001', 'Analista',        'Carga y revisión de documentos KYC'),
('a1b2c3d4-0001-4000-8000-000000000001', 'Revisor',         'Aprobación de expedientes de clientes');

-- Permisos del rol Administrador (tenant A)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name      = 'Administrador'
AND    p.code      IN ('DOC_VIEW','DOC_UPLOAD','EXP_APPROVE','USER_ADMIN');

-- Permisos del rol Analista (tenant A)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name      = 'Analista'
AND    p.code      IN ('DOC_VIEW','DOC_UPLOAD');

-- Permisos del rol Revisor (tenant A)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name      = 'Revisor'
AND    p.code      IN ('DOC_VIEW','EXP_APPROVE');

-- ── TENANT B — Financiera Andes SA ───────────────────────────────────────────

INSERT INTO Roles (tenant_id, name, description) VALUES
('a1b2c3d4-0002-4000-8000-000000000002', 'Administrador',  'Gestión completa de usuarios del tenant'),
('a1b2c3d4-0002-4000-8000-000000000002', 'Oficial KYC',    'Carga y aprobación de expedientes de diligencia'),
('a1b2c3d4-0002-4000-8000-000000000002', 'Consultor',      'Acceso de solo lectura a documentos');

-- Permisos del rol Administrador (tenant B)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name      = 'Administrador'
AND    p.code      IN ('DOC_VIEW','DOC_UPLOAD','EXP_APPROVE','USER_ADMIN');

-- Permisos del rol Oficial KYC (tenant B)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name      = 'Oficial KYC'
AND    p.code      IN ('DOC_VIEW','DOC_UPLOAD','EXP_APPROVE');

-- Permisos del rol Consultor (tenant B)
INSERT INTO Role_Permissions (role_id, permission_id)
SELECT r.id, p.id
FROM   Roles r, Permissions p
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name      = 'Consultor'
AND    p.code      IN ('DOC_VIEW');

-- =============================================================================
-- 4. USUARIOS
-- =============================================================================
-- IMPORTANTE: external_id simula el valor real que retornaría el IdP:
--   • Microsoft: claim 'oid'  (GUID del usuario en Entra ID)
--   • Google:    claim 'sub'  (número entero grande como string)

-- ── TENANT A — Banco Meridian CR (dominio: meridian.cr) ──────────────────────

INSERT INTO Users (id, tenant_id, external_id, email, full_name, status_id, last_login)
VALUES
-- Administrador activo, con último login
(
    'c1000000-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'msft-oid-00000000-aaaa-bbbb-cccc-000000000001',    -- 'oid' de Entra ID
    'admin@meridian.cr',
    'Ana Rodríguez Vargas',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NOW() - INTERVAL '2 hours'
),
-- Analista activo
(
    'c1000000-0001-4000-8000-000000000002',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'msft-oid-00000000-aaaa-bbbb-cccc-000000000002',
    'analista@meridian.cr',
    'Carlos Jiménez Mora',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NOW() - INTERVAL '1 day'
),
-- Revisor activo
(
    'c1000000-0001-4000-8000-000000000003',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'msft-oid-00000000-aaaa-bbbb-cccc-000000000003',
    'revisor@meridian.cr',
    'Sofía Campos Solano',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NULL
),
-- Analista INACTIVO (para probar rechazo en login)
(
    'c1000000-0001-4000-8000-000000000004',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'msft-oid-00000000-aaaa-bbbb-cccc-000000000004',
    'inactivo@meridian.cr',
    'Pedro Alvarado Sanchez',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='INACTIVE'),
    NULL
);

-- ── TENANT B — Financiera Andes SA (dominio: andesfinanciera.cr) ─────────────

INSERT INTO Users (id, tenant_id, external_id, email, full_name, status_id, last_login)
VALUES
-- Administrador activo
(
    'c2000000-0002-4000-8000-000000000001',
    'a1b2c3d4-0002-4000-8000-000000000002',
    '109876543210987654321',                           -- 'sub' de Google (21 dígitos)
    'admin@andesfinanciera.cr',
    'Marcela Torres Ureña',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NOW() - INTERVAL '30 minutes'
),
-- Oficial KYC activo
(
    'c2000000-0002-4000-8000-000000000002',
    'a1b2c3d4-0002-4000-8000-000000000002',
    '118765432109876543210',
    'oficial.kyc@andesfinanciera.cr',
    'Luis Hernández Quirós',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NOW() - INTERVAL '3 hours'
),
-- Consultor activo
(
    'c2000000-0002-4000-8000-000000000003',
    'a1b2c3d4-0002-4000-8000-000000000002',
    '127654321098765432109',
    'consultor@andesfinanciera.cr',
    'Valeria Núñez Blanco',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='ACTIVE'),
    NULL
),
-- Usuario INACTIVO (para probar rechazo en login)
(
    'c2000000-0002-4000-8000-000000000004',
    'a1b2c3d4-0002-4000-8000-000000000002',
    '136543210987654321098',
    'baja@andesfinanciera.cr',
    'Roberto Céspedes Mora',
    (SELECT id FROM Cat_Statuses WHERE group_name='USER' AND code='INACTIVE'),
    NULL
);

-- =============================================================================
-- 5. ASIGNACIÓN DE ROLES A USUARIOS
-- =============================================================================

-- ── TENANT A — Banco Meridian CR ─────────────────────────────────────────────

-- Ana → Administrador
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c1000000-0001-4000-8000-000000000001', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name = 'Administrador';

-- Carlos → Analista
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c1000000-0001-4000-8000-000000000002', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name = 'Analista';

-- Sofía → Revisor
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c1000000-0001-4000-8000-000000000003', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name = 'Revisor';

-- Pedro (inactivo) → Analista (el rol existe pero el usuario no puede logearse)
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c1000000-0001-4000-8000-000000000004', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0001-4000-8000-000000000001'
AND    r.name = 'Analista';

-- ── TENANT B — Financiera Andes SA ───────────────────────────────────────────

-- Marcela → Administrador
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c2000000-0002-4000-8000-000000000001', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name = 'Administrador';

-- Luis → Oficial KYC
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c2000000-0002-4000-8000-000000000002', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name = 'Oficial KYC';

-- Valeria → Consultor
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c2000000-0002-4000-8000-000000000003', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name = 'Consultor';

-- Roberto (inactivo) → Oficial KYC
INSERT INTO User_Roles (user_id, role_id)
SELECT 'c2000000-0002-4000-8000-000000000004', r.id
FROM   Roles r
WHERE  r.tenant_id = 'a1b2c3d4-0002-4000-8000-000000000002'
AND    r.name = 'Oficial KYC';

-- =============================================================================
-- 6. REGISTROS DE AUDITORÍA DE EJEMPLO
-- =============================================================================

INSERT INTO Audit_Logs (tenant_id, user_id, action, entity_name, entity_id, old_values, new_values, ip_address, timestamp)
VALUES
-- Login exitoso de Ana (Meridian)
(
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1000000-0001-4000-8000-000000000001',
    'LOGIN',
    'User',
    'c1000000-0001-4000-8000-000000000001',
    NULL,
    '{"detail":"Login exitoso"}',
    '190.10.20.100',
    NOW() - INTERVAL '2 hours'
),
-- Login fallido — usuario no provisionado (dominio meridian.cr)
(
    'a1b2c3d4-0001-4000-8000-000000000001',
    NULL,
    'LOGIN_FAILED',
    'User',
    NULL,
    NULL,
    '{"detail":"Usuario no provisionado: desconocido@meridian.cr"}',
    '190.10.20.101',
    NOW() - INTERVAL '1 hour 45 minutes'
),
-- Aprovisionamiento de Carlos por Ana
(
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1000000-0001-4000-8000-000000000001',
    'USER_CREATED',
    'User',
    'c1000000-0001-4000-8000-000000000002',
    NULL,
    '{"userId":"c1000000-0001-4000-8000-000000000002","email":"analista@meridian.cr","roles":[2]}',
    '190.10.20.100',
    NOW() - INTERVAL '5 days'
),
-- Desactivación de Pedro por Ana
(
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1000000-0001-4000-8000-000000000001',
    'USER_STATUS_CHANGED',
    'User',
    'c1000000-0001-4000-8000-000000000004',
    '{"status":"ACTIVE"}',
    '{"status":"INACTIVE"}',
    '190.10.20.100',
    NOW() - INTERVAL '2 days'
),
-- Login exitoso de Marcela (Andes — Google)
(
    'a1b2c3d4-0002-4000-8000-000000000002',
    'c2000000-0002-4000-8000-000000000001',
    'LOGIN',
    'User',
    'c2000000-0002-4000-8000-000000000001',
    NULL,
    '{"detail":"Login exitoso"}',
    '201.55.66.77',
    NOW() - INTERVAL '30 minutes'
),
-- Asignación de permisos al rol Oficial KYC por Marcela
(
    'a1b2c3d4-0002-4000-8000-000000000002',
    'c2000000-0002-4000-8000-000000000001',
    'ROLE_PERMISSIONS_UPDATED',
    'Role',
    NULL,
    NULL,
    '{"permissions":[1,2,3]}',
    '201.55.66.77',
    NOW() - INTERVAL '4 days'
);

COMMIT;

-- =============================================================================
-- 7. VERIFICACIÓN RÁPIDA (ejecutar luego del seed)
-- =============================================================================

-- Descomentar para validar el contenido insertado:
/*
SELECT
    t.name                                           AS tenant,
    t.domain_name,
    tac.provider_type                                AS idp,
    u.email,
    u.full_name,
    cs.code                                          AS status,
    STRING_AGG(r.name, ', ' ORDER BY r.name)        AS roles,
    STRING_AGG(DISTINCT p.code, ', ' ORDER BY p.code) AS permisos_efectivos
FROM Tenants             t
JOIN Tenant_Auth_Configs tac ON tac.tenant_id = t.id AND tac.is_active
JOIN Users               u   ON u.tenant_id   = t.id
JOIN Cat_Statuses        cs  ON cs.id          = u.status_id
LEFT JOIN User_Roles     ur  ON ur.user_id     = u.id
LEFT JOIN Roles          r   ON r.id           = ur.role_id
LEFT JOIN Role_Permissions rp ON rp.role_id   = r.id
LEFT JOIN Permissions    p   ON p.id           = rp.permission_id
WHERE t.identification_number IN ('3-101-123456','3-102-654321')
GROUP BY t.name, t.domain_name, tac.provider_type, u.email, u.full_name, cs.code
ORDER BY t.name, u.email;
*/
