using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

// --- DTOs (Request / Response) ---
public class DeleteElementoRequest { }
public class DeleteElementoResponse { }

// --- Endpoint / Controlador ---
public static class DeleteElementoEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/elementos/{id}", async (string id, DeleteElementoHandler handler, HttpContext http) => await handler.HandleAsync(id, http))
            .RequireAuthorization()
            .WithTags("Elementos")
            .WithSummary("Eliminar un elemento del inventario")
            .WithDescription("Elimina un elemento si el usuario es propietario o administrador.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

// --- Lógica de Negocio (Handler) ---
public class DeleteElementoHandler
{
    private readonly ApplicationDbContext _db;

    public DeleteElementoHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync(string id, HttpContext http)
    {
        if (!Guid.TryParse(id, out var elementoId))
            return Results.BadRequest("Id inválido.");

        var elemento = await _db.Elementos.FirstOrDefaultAsync(e => e.Id == elementoId);
        if (elemento == null)
            return Results.NotFound();

        var userId = GetLoggedUserId(http);
        if (userId == null)
            return Results.Forbid();

        var role = GetLoggedUserRole(http);
        if (elemento.UsuarioIdPropietario != userId.Value && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        _db.Elementos.Remove(elemento);
        await _db.SaveChangesAsync();

        return Results.NoContent();
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
