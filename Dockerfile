# ══════════════════════════════════════════════════════════════════════════════
# Stage 1: Build — SDK de .NET 10
# ══════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copiar solution y archivos de proyecto para aprovechar el cache de capas de Docker
COPY ["src/IdentityAdministration.slnx", "src/"]
COPY ["src/IdentityAdministration.Domain/IdentityAdministration.Domain.csproj",        "src/IdentityAdministration.Domain/"]
COPY ["src/IdentityAdministration.Application/IdentityAdministration.Application.csproj", "src/IdentityAdministration.Application/"]
COPY ["src/IdentityAdministration.Infrastructure/IdentityAdministration.Infrastructure.csproj", "src/IdentityAdministration.Infrastructure/"]
COPY ["src/IdentityAdministration.API/IdentityAdministration.API.csproj",              "src/IdentityAdministration.API/"]

# Restaurar dependencias (capa cacheada si los .csproj no cambian)
RUN dotnet restore "src/IdentityAdministration.API/IdentityAdministration.API.csproj"

# Copiar el resto del código fuente
COPY src/ src/

# Publicar en modo Release (optimizado, sin pdb en producción)
RUN dotnet publish "src/IdentityAdministration.API/IdentityAdministration.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ══════════════════════════════════════════════════════════════════════════════
# Stage 2: Runtime — imagen ASP.NET Core de .NET 10 (sin SDK)
# ══════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Crear usuario no-root para ejecución segura en AKS
RUN groupadd --system --gid 1001 sgedgroup \
    && useradd --system --uid 1001 --gid sgedgroup --no-create-home sgeduser

# Copiar los artefactos publicados con ownership del usuario no-root
COPY --from=build --chown=sgeduser:sgedgroup /app/publish .

# Cambiar al usuario no-root
USER sgeduser

# Exponer el puerto HTTP (Kubernetes Service redirige el tráfico aquí)
EXPOSE 8080

# Variables de entorno de .NET
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Health check para Docker / Kubernetes
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "IdentityAdministration.API.dll"]
