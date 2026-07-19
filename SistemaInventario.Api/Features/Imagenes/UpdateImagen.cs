using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Imagenes;

// --- DTOs (Request / Response) ---
public class UpdateImagenRequest
{
    public IFormFile Archivo { get; set; } = default!;
}

public class UpdateImagenResponse
{
    public string Id { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class UpdateImagenEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/imagenes/{id}", async (string id, [FromForm] UpdateImagenRequest request, UpdateImagenHandler handler, HttpContext http) => await handler.HandleAsync(id, request, http))
            .DisableAntiforgery()
            .RequireAuthorization()
            .WithTags("Imagenes")
            .WithSummary("Reemplazar el archivo de una imagen existente")
            .WithDescription("Sustituye el archivo físico de una imagen ya registrada, conservando su Id.")
            .Produces<UpdateImagenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

// --- Lógica de Negocio (Handler) ---
public class UpdateImagenHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ImagenStorage _storage;

    public UpdateImagenHandler(ApplicationDbContext db, ImagenStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IResult> HandleAsync(string id, UpdateImagenRequest request, HttpContext http)
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

        var archivo = request.Archivo;
        if (archivo == null)
            return Results.BadRequest("Se requiere un archivo con el nombre 'Archivo'.");

        if (archivo.Length <= 0 || archivo.Length > ImagenReglas.MaxFileSizeBytes)
            return Results.BadRequest("El archivo es demasiado grande o está vacío.");

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!ImagenReglas.ExtensionesPermitidas.Contains(extension) || !archivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("El archivo debe ser una imagen (jpg, jpeg, png, gif o webp).");

        var nombreAnterior = imagen.NombreArchivo;
        var nombreNuevo = await _storage.GuardarAsync(archivo);

        imagen.NombreArchivo = nombreNuevo;
        imagen.ContentType = archivo.ContentType;

        _db.Imagenes.Update(imagen);
        await _db.SaveChangesAsync();

        _storage.Eliminar(nombreAnterior);

        return Results.Ok(new UpdateImagenResponse { Id = imagen.Id.ToString() });
    }
}
