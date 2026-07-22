using System;
using System.IO;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;
using SistemaInventario.Api.Infrastructure.Database;
using SistemaInventario.Api.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
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

// Ensure images folder exists (from configuration)
var imagesRelativePath = builder.Configuration.GetValue<string>("FileStorage:ImagesPath")?.Trim() ?? "wwwroot/images/";
var imagesAbsolutePath = Path.GetFullPath(imagesRelativePath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(imagesAbsolutePath);

// Registrar los handlers en el contenedor de dependencias
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

// Database (in-memory for Phase 1)
// 1. Obtienes el string de conexión de tu appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Condicionas el tipo de base de datos según el entorno
if (builder.Environment.IsDevelopment())
{
    // Si estás en Development, usa la base de datos en memoria
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("InventarioDbMock"));
}
else
{
    // Si estás en Production (o cualquier otro), usa SQL Server con tu conexión real
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}




// Security
builder.Services.AddSingleton<IJwtProvider, JwtProvider>();
// Register a passive authentication scheme that defers to the populated HttpContext.User
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Passive";
    options.DefaultChallengeScheme = "Passive";
    options.DefaultAuthenticateScheme = "Passive";
})
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SistemaInventario.Api.Infrastructure.Security.PassiveAuthenticationHandler>(
        "Passive", _ => { });
// No external JWT middleware added; use internal JwtValidationMiddleware instead.
builder.Services.AddAuthorization();

// Blockchain logger provider: copia cada ILogger también a la red Ethereum Sepolia si se configura la llave privada.
builder.Logging.AddProvider(new BlockchainLoggerProvider(builder.Configuration));

// Swagger / OpenAPI
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

    // FILTRO: Quitar candados a los endpoints sin autenticación
    options.OperationFilter<SistemaInventario.Api.Infrastructure.Security.QuitarCandadoFiltro>();
    
    // FILTRO: Documentar automáticamente la respuesta 401 en endpoints autorizados
    options.OperationFilter<SistemaInventario.Api.Infrastructure.Security.DocumentarUnauthorizedFiltro>();
});
// Keep any existing AddOpenApi extension if present
try
{
    builder.Services.AddOpenApi();
}
catch { /* ignore if AddOpenApi not available */ }


//cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("PoliticaFrontend", policy =>
    {
        // Reemplaza con la URL y puerto exacto donde corre tu entorno de desarrollo web (ej. Vite/SvelteKit)
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});



var app = builder.Build();

// Enable Swagger UI for all environments so the health endpoint is discoverable
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sistema Inventario API V1"));

// Map legacy openapi if available
try
{
    app.MapOpenApi();
}
catch { /* ignore if MapOpenApi not available */ }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("PoliticaFrontend");
// Use in-repo JWT validator middleware to populate HttpContext.User when valid Bearer token provided
app.UseJwtValidation();
app.UseAuthorization();

// Health endpoint (API check)
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
// MAPEO DE ENDPOINTS - MÓDULO USUARIOS
// ============================================================
// Mapear las rutas de autenticación
SistemaInventario.Api.Features.Auth.RegistroEndpoint.Map(app);
SistemaInventario.Api.Features.Auth.LoginEndpoint.Map(app);

// Mapear rutas de Revisiones
SistemaInventario.Api.Features.Revisiones.CrearRevision.Map(app);
SistemaInventario.Api.Features.Revisiones.GetRevisiones.Map(app);
SistemaInventario.Api.Features.Revisiones.GetRevisionById.Map(app);
SistemaInventario.Api.Features.Revisiones.EscanearCodigo.Map(app);
SistemaInventario.Api.Features.Revisiones.FinalizarRevision.Map(app);

// Mapear rutas de Elementos
SistemaInventario.Api.Features.Elementos.GetElementosEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.GetElementoByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.CreateElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.UpdateElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.DeleteElementoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.ImportarMasivoEndpoint.Map(app);
SistemaInventario.Api.Features.Elementos.ExportarExcelEndpoint.Map(app);
// Mapear rutas de Usuarios
SistemaInventario.Api.Features.Usuarios.GetUsuariosEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.GetUsuarioByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.UpdateUsuarioEndpoint.Map(app);
SistemaInventario.Api.Features.Usuarios.DeleteUsuarioEndpoint.Map(app);

// Mapear rutas de Imagenes
SistemaInventario.Api.Features.Imagenes.CreateImagenEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.GetImagenByIdEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.UpdateImagenEndpoint.Map(app);
SistemaInventario.Api.Features.Imagenes.DeleteImagenEndpoint.Map(app);

app.Logger.LogInformation("Blockchain startup test log: enviando prueba de log a la red.");

app.Run();

