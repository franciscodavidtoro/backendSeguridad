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
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
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
            if (string.IsNullOrWhiteSpace(request.CodigoBarras) || string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Categoria))
                return Results.BadRequest("Código de barras, nombre y categoría son obligatorios.");

            if (request.Precio < 0)
                return Results.BadRequest("El precio no puede ser negativo.");

            var userId = GetLoggedUserId(http);
            if (userId == null)
                return Results.Forbid();

            var codigoNormalizado = request.CodigoBarras.Trim();
            var existe = await _db.Elementos.AnyAsync(e => e.CodigoBarras == codigoNormalizado);
            if (existe)
                return Results.Conflict("Ya existe un elemento con ese código de barras.");

            var elemento = new Elemento
            {
                Id = Guid.NewGuid(),
                CodigoBarras = codigoNormalizado,
                Nombre = request.Nombre.Trim(),
                Descripcion = request.Descripcion?.Trim(),
                Categoria = request.Categoria.Trim(),
                Precio = request.Precio,
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

