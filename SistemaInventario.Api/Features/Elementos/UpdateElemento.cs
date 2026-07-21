using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Domain.Entities;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

// --- DTOs (Request / Response) ---
public class UpdateElementoRequest
{
    public string CodigoBien { get; set; } = string.Empty;
    public string NombreBien { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Modelo { get; set; }
    public string? MarcaRazaOtros { get; set; }
    public string? Ubicacion { get; set; }
    public string? RutaImagen { get; set; }
}

public class UpdateElementoResponse
{
    public string Id { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class UpdateElementoEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/elementos/{id}", async (string id, UpdateElementoRequest request, UpdateElementoHandler handler, HttpContext http) => await handler.HandleAsync(id, request, http))
            .RequireAuthorization()
            .WithTags("Elementos")
            .WithSummary("Actualizar un elemento del inventario")
            .WithDescription("Modifica los datos de un elemento si el usuario es propietario o administrador.")
            .Produces<UpdateElementoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

// --- L�gica de Negocio (Handler) ---
public class UpdateElementoHandler
{
    private readonly ApplicationDbContext _db;

    public UpdateElementoHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync(string id, UpdateElementoRequest request, HttpContext http)
    {
        if (!Guid.TryParse(id, out var elementoId))
            return Results.BadRequest("Id inválido.");

        if (string.IsNullOrWhiteSpace(request.CodigoBien) || string.IsNullOrWhiteSpace(request.NombreBien))
            return Results.BadRequest("Código del bien y nombre del bien son obligatorios.");

        var elemento = await _db.Elementos.FirstOrDefaultAsync(e => e.Id == elementoId);
        if (elemento == null)
            return Results.NotFound();

        var userId = GetLoggedUserId(http);
        if (userId == null)
            return Results.Forbid();

        var role = GetLoggedUserRole(http);
        if (elemento.UsuarioIdPropietario != userId.Value && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        var codigoBien = request.CodigoBien.Trim();
        var nombreBien = request.NombreBien.Trim();

        var existeDuplicado = await _db.Elementos.AnyAsync(e => e.CodigoBien == codigoBien && e.Id != elementoId);
        if (existeDuplicado)
            return Results.Conflict("Ya existe otro elemento con ese código del bien.");

        elemento.CodigoBien = codigoBien;
        elemento.NombreBien = nombreBien;
        elemento.Serie = request.Serie?.Trim();
        elemento.Modelo = request.Modelo?.Trim();
        elemento.MarcaRazaOtros = request.MarcaRazaOtros?.Trim();
        elemento.Ubicacion = request.Ubicacion?.Trim();
        elemento.RutaImagen = request.RutaImagen?.Trim();

        _db.Elementos.Update(elemento);
        await _db.SaveChangesAsync();

        return Results.Ok(new UpdateElementoResponse { Id = elemento.Id.ToString() });
    }

    private Guid? GetLoggedUserId(HttpContext http)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (claim == null || !Guid.TryParse(claim.Value, out var usuarioId))
            return null;
        return usuarioId;
    }

    private string GetLoggedUserRole(HttpContext http)
    {
        return http.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }
}
