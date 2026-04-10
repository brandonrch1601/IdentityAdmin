-- Habilitar extensión para identificadores únicos universales
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

---------------------------------------------------------
-- 1. CATÁLOGOS DE SISTEMA Y ESTADOS
---------------------------------------------------------

CREATE TABLE Cat_Statuses (
    id SERIAL PRIMARY KEY,
    group_name VARCHAR(50) NOT NULL, -- 'TENANT', 'USER', 'CUSTOMER', 'DOCUMENT'
    code VARCHAR(20) NOT NULL,
    description VARCHAR(100) NOT NULL,
    UNIQUE(group_name, code)
);

CREATE TABLE Permissions (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL, -- 'DOC_VIEW', 'DOC_APPROVE', etc.
    description VARCHAR(255) NOT NULL
);

---------------------------------------------------------
-- 2. CATÁLOGOS DE CUMPLIMIENTO (KYC - COSTA RICA)
---------------------------------------------------------

CREATE TABLE Cat_Identification_Types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL, -- 'FISICA', 'JURIDICA', 'DIMEX', 'PASAPORTE'
    description VARCHAR(100) NOT NULL,
    validation_regex VARCHAR(255)
);

CREATE TABLE Cat_Customer_Types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL, -- 'PHYS', 'LEGAL'
    description VARCHAR(100) NOT NULL
);

CREATE TABLE Cat_Risk_Profiles (
    id SERIAL PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL, -- 'LOW', 'MEDIUM', 'HIGH'
    description VARCHAR(100) NOT NULL
);

CREATE TABLE Cat_Relationship_Types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(20) UNIQUE NOT NULL, -- 'REP_LEGAL', 'SHAREHOLDER', 'BENEF_OWNER'
    description VARCHAR(100) NOT NULL
);

CREATE TABLE Cat_Document_Types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL, 
    description VARCHAR(100) NOT NULL
);

-- División Territorial Administrativa (Provincias, Cantones, Distritos)
CREATE TABLE Cat_Geographic_Locations (
    id SERIAL PRIMARY KEY,
    parent_id INTEGER REFERENCES Cat_Geographic_Locations(id),
    level VARCHAR(20) NOT NULL, -- 'PROVINCIA', 'CANTON', 'DISTRITO'
    code VARCHAR(10) NOT NULL,
    name VARCHAR(100) NOT NULL,
    UNIQUE(level, code)
);

-- Actividades Económicas (Código CIUU)
CREATE TABLE Cat_Economic_Activities (
    id SERIAL PRIMARY KEY,
    code VARCHAR(20) UNIQUE NOT NULL, 
    description TEXT NOT NULL,
    risk_weight DECIMAL(5,2) DEFAULT 1.0
);

---------------------------------------------------------
-- 3. ADMINISTRACIÓN DEL SAAS (TENANTS)
---------------------------------------------------------

CREATE TABLE Tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    identification_number VARCHAR(50) UNIQUE NOT NULL,
    domain_name VARCHAR(100) UNIQUE NOT NULL, -- Para Home Realm Discovery
    status_id INTEGER REFERENCES Cat_Statuses(id),
    branding_config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Configuración de Identidad (Entra ID / Google)
CREATE TABLE Tenant_Auth_Configs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    provider_type VARCHAR(20) NOT NULL, -- 'MICROSOFT', 'GOOGLE'
    client_id TEXT NOT NULL,
    issuer_url TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    UNIQUE(tenant_id, provider_type)
);

---------------------------------------------------------
-- 4. SEGURIDAD Y CONTROL DE ACCESO (RBAC)
---------------------------------------------------------

CREATE TABLE Roles (
    id SERIAL PRIMARY KEY,
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    UNIQUE(tenant_id, name)
);

CREATE TABLE Role_Permissions (
    role_id INTEGER REFERENCES Roles(id) ON DELETE CASCADE,
    permission_id INTEGER REFERENCES Permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE Users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    external_id VARCHAR(255) UNIQUE NOT NULL, -- OID de Azure o Sub de Google
    email VARCHAR(255) NOT NULL,
    full_name VARCHAR(255),
    license_seat BOOLEAN NOT NULL DEFAULT TRUE, -- Controla si ocupa una licencia activa
    status_id INTEGER REFERENCES Cat_Statuses(id),
    last_login TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE User_Roles (
    user_id UUID REFERENCES Users(id) ON DELETE CASCADE,
    role_id INTEGER REFERENCES Roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

---------------------------------------------------------
-- 5. NEGOCIO: CLIENTES Y EXPEDIENTES
---------------------------------------------------------

CREATE TABLE Customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    identification_number VARCHAR(50) NOT NULL,
    identification_type_id INTEGER REFERENCES Cat_Identification_Types(id),
    customer_type_id INTEGER REFERENCES Cat_Customer_Types(id),
    risk_profile_id INTEGER REFERENCES Cat_Risk_Profiles(id),
    economic_activity_id INTEGER REFERENCES Cat_Economic_Activities(id),
    district_id INTEGER REFERENCES Cat_Geographic_Locations(id), -- Punto final DTA
    status_id INTEGER REFERENCES Cat_Statuses(id),
    basic_info JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(tenant_id, identification_number)
);

-- Relaciones para Personas Jurídicas (Árbol de Socios)
CREATE TABLE Customer_Relationships (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    parent_customer_id UUID REFERENCES Customers(id),
    child_customer_id UUID REFERENCES Customers(id),
    relationship_type_id INTEGER REFERENCES Cat_Relationship_Types(id),
    ownership_percentage DECIMAL(5,2),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE Documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES Tenants(id) ON DELETE CASCADE,
    customer_id UUID REFERENCES Customers(id) ON DELETE CASCADE,
    document_type_id INTEGER REFERENCES Cat_Document_Types(id),
    status_id INTEGER REFERENCES Cat_Statuses(id),
    storage_path TEXT NOT NULL,
    expiration_date DATE,
    ocr_result JSONB DEFAULT '{}',
    is_signed BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

---------------------------------------------------------
-- 6. TRAZABILIDAD Y AUDITORÍA
---------------------------------------------------------

CREATE TABLE Audit_Logs (
    id BIGSERIAL PRIMARY KEY,
    tenant_id UUID REFERENCES Tenants(id),
    user_id UUID,
    action VARCHAR(100) NOT NULL,
    entity_name VARCHAR(50) NOT NULL,
    entity_id UUID,
    old_values JSONB,
    new_values JSONB,
    ip_address VARCHAR(45),
    timestamp TIMESTAMPTZ DEFAULT NOW()
);

---------------------------------------------------------
-- 7. OPTIMIZACIÓN (ÍNDICES)
---------------------------------------------------------

CREATE INDEX idx_customers_iso ON Customers(tenant_id, identification_number);
CREATE INDEX idx_docs_customer ON Documents(customer_id);
CREATE INDEX idx_audit_iso ON Audit_Logs(tenant_id, timestamp DESC);
CREATE INDEX idx_cust_json ON Customers USING GIN (basic_info);
CREATE INDEX idx_docs_ocr ON Documents USING GIN (ocr_result);

---------------------------------------------------------
-- 8. SEMILLAS INICIALES (VALORES BASE)
---------------------------------------------------------

-- Estados
INSERT INTO Cat_Statuses (group_name, code, description) VALUES 
('TENANT', 'ACTIVE', 'Activo'), ('TENANT', 'SUSPENDED', 'Suspendido'),
('USER', 'ACTIVE', 'Activo'), ('USER', 'INACTIVE', 'Inactivo'),
('CUSTOMER', 'ACTIVE', 'Activo'), ('CUSTOMER', 'INACTIVE', 'Inactivo'),
('DOCUMENT', 'PENDING', 'Pendiente de Revisión'), ('DOCUMENT', 'VALID', 'Válido'), 
('DOCUMENT', 'EXPIRED', 'Vencido'), ('DOCUMENT', 'REJECTED', 'Rechazado');

-- Identificación
INSERT INTO Cat_Identification_Types (code, description) VALUES 
('FISICA', 'Cédula Física'), ('JURIDICA', 'Cédula Jurídica'), 
('DIMEX', 'DIMEX'), ('PASAPORTE', 'Pasaporte');

-- Tipos de Cliente y Riesgo
INSERT INTO Cat_Customer_Types (code, description) VALUES ('PHYS', 'Persona Física'), ('LEGAL', 'Persona Jurídica');
INSERT INTO Cat_Risk_Profiles (code, description) VALUES ('LOW', 'Bajo'), ('MEDIUM', 'Medio'), ('HIGH', 'Alto');

-- Permisos
INSERT INTO Permissions (code, description) VALUES 
('DOC_VIEW', 'Ver documentos'), ('DOC_UPLOAD', 'Subir documentos'), 
('EXP_APPROVE', 'Aprobar expedientes'), ('USER_ADMIN', 'Administrar usuarios');