    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using System.Security.Claims;
    using Microsoft.EntityFrameworkCore;
    using SistemaInventario.Api.Domain.Entities;
    using SistemaInventario.Api.Infrastructure.Database;

    namespace SistemaInventario.Api.Features.Elementos;

    // --- DTOs (Request / Response) ---
    public class CreateElementoRequest
    {
        public string CodigoBien { get; set; } = string.Empty;
        public string NombreBien { get; set; } = string.Empty;
        public string? Serie { get; set; }
        public string? Modelo { get; set; }
        public string? MarcaRazaOtros { get; set; }
        public string? Ubicacion { get; set; }
        public string? RutaImagen { get; set; }
    }

    public class CreateElementoResponse
    {
        public string Id { get; set; } = string.Empty;
    }

    // --- Endpoint / Controlador ---
    public static class CreateElementoEndpoint
    {
        public static void Map(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/elementos", async (CreateElementoRequest request, CreateElementoHandler handler, HttpContext http) => await handler.HandleAsync(request, http))
                .RequireAuthorization()
                .WithTags("Elementos")
                .WithSummary("Crear un nuevo elemento del inventario")
                .WithDescription("Registra un nuevo elemento en el inventario y lo asigna al usuario autenticado.")
                .Produces<CreateElementoResponse>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status409Conflict);
        }
    }

    // --- Lógica de Negocio (Handler) ---
    public class CreateElementoHandler
    {
        private readonly ApplicationDbContext _db;

        public CreateElementoHandler(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IResult> HandleAsync(CreateElementoRequest request, HttpContext http)
        {
            if (string.IsNullOrWhiteSpace(request.CodigoBien) || string.IsNullOrWhiteSpace(request.NombreBien))
                return Results.BadRequest("Código del bien y nombre del bien son obligatorios.");

            var userId = GetLoggedUserId(http);
            if (userId == null)
                return Results.Forbid();

            var codigoBien = request.CodigoBien.Trim();
            var nombreBien = request.NombreBien.Trim();

            var existeDuplicado = await _db.Elementos.AnyAsync(e => e.CodigoBien == codigoBien);
            if (existeDuplicado)
                return Results.Conflict("Ya existe un elemento con ese código del bien.");

            var elemento = new Elemento
            {
                Id = Guid.NewGuid(),
                CodigoBien = codigoBien,
                NombreBien = nombreBien,
                Serie = request.Serie?.Trim(),
                Modelo = request.Modelo?.Trim(),
                MarcaRazaOtros = request.MarcaRazaOtros?.Trim(),
                Ubicacion = request.Ubicacion?.Trim(),
                RutaImagen = request.RutaImagen?.Trim(),
                UsuarioIdPropietario = userId.Value
            };

            await _db.Elementos.AddAsync(elemento);
            await _db.SaveChangesAsync();

            return Results.Created($"/api/elementos/{elemento.Id}", new CreateElementoResponse { Id = elemento.Id.ToString() });
        }

        private Guid? GetLoggedUserId(HttpContext http)
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
                return null;

            return usuarioId;
        }
    }

