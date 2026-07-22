using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Nethereum.Signer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using SistemaInventario.Api.Infrastructure.Database;
using SistemaInventario.Api.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. VALIDACIONES Y CONFIGURACIÓN INICIAL (TUYAS + COMPAÑERO)
// ============================================================

// [TUYO] Validación estricta del JWT
if (string.IsNullOrWhiteSpace(builder.Configuration["JwtSettings:SecretKey"]))
{
    throw new InvalidOperationException(
        "No se configuró JwtSettings:SecretKey. En desarrollo, defínala con " +
        "'dotnet user-secrets set \"JwtSettings:SecretKey\" \"<valor>\"'. En producción, use la " +
        "variable de entorno JwtSettings__SecretKey (doble guion bajo).");
}

// [COMPAÑERO] Configuración de Blockchain
var blockchainConfig = builder.Configuration.GetSection("BlockchainLogging").Get<BlockchainLoggerOptions>() ?? new BlockchainLoggerOptions();
if (string.IsNullOrWhiteSpace(blockchainConfig.PrivateKey))
{
    var ecKey = EthECKey.GenerateKey();
    var address = ecKey.GetPublicAddress();
    var privateKey = ecKey.GetPrivateKey();
    var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("BlockchainKeyGenerator");
    startupLogger.LogInformation("Blockchain key pair generated. Address: {Address}", address);
    startupLogger.LogInformation("Blockchain private key generated. PrivateKey: {PrivateKey}", privateKey);
}

// Carpeta de imágenes
var imagesRelativePath = builder.Configuration.GetValue<string>("FileStorage:ImagesPath")?.Trim() ?? "wwwroot/images/";
var imagesAbsolutePath = Path.GetFullPath(imagesRelativePath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(imagesAbsolutePath);

// ============================================================
// 2. INYECCIÓN DE DEPENDENCIAS (HANDLERS Y SERVICIOS)
// ============================================================
builder.Services.AddScoped<SistemaInventario.Api.Features.Auth.RegistroHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Auth.LoginHandler>();

builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.GetElementosHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.GetElementoByIdHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.CreateElementoHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.UpdateElementoHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.DeleteElementoHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.ImportarMasivoHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Elementos.ExportarExcelHandler>();

builder.Services.AddSingleton<SistemaInventario.Api.Features.Imagenes.ImagenStorage>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Imagenes.CreateImagenHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Imagenes.GetImagenByIdHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Imagenes.UpdateImagenHandler>();
builder.Services.AddScoped<SistemaInventario.Api.Features.Imagenes.DeleteImagenHandler>();

// [TUYO] OAuth 2.0 (Google) — cliente HTTP para el intercambio de código por tokens
builder.Services.AddHttpClient("GoogleOAuth");
builder.Services.AddSingleton(_ => new ConfigurationManager<OpenIdConnectConfiguration>(
    "https://accounts.google.com/.well-known/openid-configuration",
    new OpenIdConnectConfigurationRetriever(),
    new HttpClient()));

// ============================================================
// 3. BASE DE DATOS
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("InventarioDbMock"));
}
else
{
    // [TUYO] Validación estricta para producción
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "No se configuró ConnectionStrings:DefaultConnection. Defina la variable de entorno " +
            "ConnectionStrings__DefaultConnection (doble guion bajo) con la cadena de conexión real " +
            "antes de ejecutar fuera de Development.");
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// ============================================================
// 4. SEGURIDAD Y LOGGING
// ============================================================
builder.Services.AddSingleton<IJwtProvider, JwtProvider>();

// [FUSIONADO] Esquema pasivo + Negotiate de tu compañero
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Passive";
    options.DefaultChallengeScheme = "Passive";
    options.DefaultAuthenticateScheme = "Passive";
})
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SistemaInventario.Api.Infrastructure.Security.PassiveAuthenticationHandler>(
        "Passive", _ => { })
    .AddNegotiate(); // <-- Esto lo hizo tu compañero

builder.Services.AddAuthorization();

// [COMPAÑERO] Blockchain logger provider
builder.Logging.AddProvider(new BlockchainLoggerProvider(builder.Configuration));

// ============================================================
// 5. SWAGGER Y CORS
// ============================================================
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Pega tu token JWT aquí.\n\nNota: No es necesario escribir 'Bearer ' al inicio."
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });

    options.OperationFilter<SistemaInventario.Api.Infrastructure.Security.QuitarCandadoFiltro>();
    options.OperationFilter<SistemaInventario.Api.Infrastructure.Security.DocumentarUnauthorizedFiltro>();
});

try { builder.Services.AddOpenApi(); } catch { }

builder.Services.AddCors(options =>
{
    options.AddPolicy("PoliticaFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================
// 6. PIPELINE DE LA APLICACIÓN
// ============================================================
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sistema Inventario API V1"));

try { app.MapOpenApi(); } catch { }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("PoliticaFrontend");

app.UseAuthentication(); // <-- Requerido por el Negotiate de tu compañero
app.UseJwtValidation();
app.UseAuthorization();

// Health endpoint
app.MapGet("/api/health", (IConfiguration config, IWebHostEnvironment env) =>
{
    var result = new
    {
        status = "Running",
        environment = env.EnvironmentName,
        imagesPath = config["FileStorage:ImagesPath"],
        timestamp = DateTime.UtcNow
    };
    return Results.Ok(result);
})
.AllowAnonymous()
.WithName("ApiHealth")
.WithOpenApi();

// ============================================================
// 7. MAPEO DE ENDPOINTS
// ============================================================
SistemaInventario.Api.Features.Auth.RegistroEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.LoginEndpoint.Map(app);

// [TUYO] Rutas MFA y OAuth
SistemaInventario.Api.Features.Auth.Mfa.MfaSetupEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.Mfa.MfaActivarEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.Mfa.MfaDesactivarEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.Mfa.MfaLoginVerificarEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.OAuth.OAuthIniciarEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.OAuth.OAuthCallbackEndpoint.Map(app);

SistemaInventario.Api.Features.Revisiones.CrearRevision.Map(app);
SistemaInventario.Api.Features.Revisiones.GetRevisiones.Map(app);
SistemaInventario.Api.Features.Revisiones.GetRevisionById.Map(app);
SistemaInventario.Api.Features.Revisiones.EscanearCodigo.Map(app);
SistemaInventario.Api.Features.Revisiones.FinalizarRevision.Map(app);

SistemaInventario.Api.Features.Elementos.GetElementosEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.GetElementoByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.CreateElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.UpdateElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.DeleteElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.ImportarMasivoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.ExportarExcelEndpoint.Map(app);

SistemaInventario.Api.Features.Usuarios.GetUsuariosEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.GetUsuarioByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.UpdateUsuarioEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.DeleteUsuarioEndpoint.Map(app);

SistemaInventario.Api.Features.Imagenes.CreateImagenEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.GetImagenByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.UpdateImagenEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.DeleteImagenEndpoint.Map(app);

// [COMPAÑERO] Log de prueba para la red Blockchain
app.Logger.LogInformation("Blockchain startup test log: enviando prueba de log a la red.");

app.Run();