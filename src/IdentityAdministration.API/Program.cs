using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IdentityAdministration.API.Authorization;
using IdentityAdministration.API.Middleware;
using IdentityAdministration.Application;
using IdentityAdministration.Infrastructure;
using IdentityAdministration.Infrastructure.Options;
using IdentityAdministration.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════════════════════════════════════════
// 1. Capas de la aplicación
// ════════════════════════════════════════════════════════════════════════════
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment.IsDevelopment());

// ════════════════════════════════════════════════════════════════════════════
// 2. JWT Bearer — valida el token interno usando la clave pública del emisor
//    La clave se carga desde AKV / LocalRSA al arrancar (ver más abajo)
// ════════════════════════════════════════════════════════════════════════════
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()!;

// Primero registramos el JwtSigningKeyProvider (ya registrado en Infrastructure)
// El token de validación se establece después de builder.Build()

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // IssuerSigningKeyResolver usa el JwtSigningKeyProvider singleton
            IssuerSigningKeyResolver = (_, _, _, _) =>
            {
                // Se resolverá en tiempo de ejecución una vez que el provider esté inicializado
                return [];
            }
        };

        // Configurar el resolver con el provider real (post-build)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var keyProvider = context.HttpContext.RequestServices
                    .GetRequiredService<JwtSigningKeyProvider>();
                context.Options.TokenValidationParameters.IssuerSigningKeyResolver =
                    (_, _, _, _) =>
                        keyProvider.IsInitialized
                            ? [keyProvider.GetPublicKeyOrThrow()]
                            : [];
                return Task.CompletedTask;
            }
        };
    });

// ════════════════════════════════════════════════════════════════════════════
// 3. Autorización — política dinámica por permisos
// ════════════════════════════════════════════════════════════════════════════
builder.Services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
{
    var fallback = new DefaultAuthorizationPolicyProvider(
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>());
    return new HasPermissionPolicyProvider(fallback);
});
builder.Services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();
builder.Services.AddAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// 4. Controllers + Response Cache
// ════════════════════════════════════════════════════════════════════════════
builder.Services.AddControllers();
builder.Services.AddResponseCaching();

// ════════════════════════════════════════════════════════════════════════════
// 5. Swagger / OpenAPI con soporte JWT Bearer
// ════════════════════════════════════════════════════════════════════════════
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Administration API",
        Version = "v1",
        Description = "Microservicio de administración de identidad multi-tenant. " +
                      "BIAN Service Domain: Identity Administration. " +
                      "Delega autenticación en Google/Microsoft y emite JWT internos con permisos RBAC.",
        Contact = new OpenApiContact { Name = "Tecasoft CR", Url = new Uri("https://teca-soft.com") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el JWT interno del SaaS obtenido de POST /auth/exchange"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

// ════════════════════════════════════════════════════════════════════════════
// 6. Build de la aplicación
// ════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ════════════════════════════════════════════════════════════════════════════
// 7. Inicialización de la clave pública JWT desde AKV (o RSA local en dev)
//    Se ejecuta ANTES de app.Run() para que las peticiones ya lleguen con la clave lista
// ════════════════════════════════════════════════════════════════════════════
await app.Services.InitializeJwtSigningKeyAsync();

// ════════════════════════════════════════════════════════════════════════════
// 8. Pipeline de middleware
// ════════════════════════════════════════════════════════════════════════════
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Administration v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseResponseCaching();
app.UseAuthentication();

// Poblamos el TenantContext con los datos del JWT (después de autenticar)
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
