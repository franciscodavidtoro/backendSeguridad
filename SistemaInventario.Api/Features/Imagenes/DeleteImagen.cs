using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Imagenes;

// --- Endpoint / Controlador ---
public static class DeleteImagenEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/imagenes/{id}", async (string id, DeleteImagenHandler handler, HttpContext http) => await handler.HandleAsync(id, http))
            .RequireAuthorization()
            .WithTags("Imagenes")
            .WithSummary("Eliminar una imagen")
            .WithDescription("Elimina el registro y el archivo físico si el usuario es quien lo subió o es administrador.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

// --- Lógica de Negocio (Handler) ---
public class DeleteImagenHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ImagenStorage _storage;

    public DeleteImagenHandler(ApplicationDbContext db, ImagenStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IResult> HandleAsync(string id, HttpContext http)
    {
        if (!Guid.TryParse(id, out var imagenId))
            return Results.BadRequest("Id inválido.");

        var imagen = await _db.Imagenes.FirstOrDefaultAsync(i => i.Id == imagenId);
        if (imagen == null)
            return Results.NotFound();

        var userId = ImagenReglas.GetLoggedUserId(http);
        if (userId == null)
            return Results.Forbid();

        var rol = ImagenReglas.GetLoggedUserRole(http);
        if (!ImagenReglas.PuedeGestionar(imagen.UsuarioIdCarga, userId.Value, rol))
            return Results.Forbid();

        _db.Imagenes.Remove(imagen);
        await _db.SaveChangesAsync();

        _storage.Eliminar(imagen.NombreArchivo);

        return Results.NoContent();
    }
}
