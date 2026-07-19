using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Imagenes;

// --- Endpoint / Controlador ---
// Nota: a propósito no existe un endpoint de listado general; solo se puede
// consultar una imagen a la vez, conociendo su Id.
public static class GetImagenByIdEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/imagenes/{id}", async (string id, GetImagenByIdHandler handler, HttpContext http) => await handler.HandleAsync(id, http))
            .RequireAuthorization()
            .WithTags("Imagenes")
            .WithSummary("Obtener el archivo de una imagen por su identificador")
            .WithDescription("Devuelve el contenido binario de una imagen específica. No existe un endpoint de listado general.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

// --- Lógica de Negocio (Handler) ---
public class GetImagenByIdHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ImagenStorage _storage;

    public GetImagenByIdHandler(ApplicationDbContext db, ImagenStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IResult> HandleAsync(string id, HttpContext http)
    {
        if (!Guid.TryParse(id, out var imagenId))
            return Results.BadRequest("Id inválido.");

        var imagen = await _db.Imagenes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imagenId);
        if (imagen == null)
            return Results.NotFound();

        var userId = ImagenReglas.GetLoggedUserId(http);
        if (userId == null)
            return Results.Forbid();

        var rutaFisica = _storage.GetFullPath(imagen.NombreArchivo);
        if (!File.Exists(rutaFisica))
            return Results.NotFound("El archivo físico ya no existe en el servidor.");

        return Results.File(rutaFisica, imagen.ContentType);
    }
}
