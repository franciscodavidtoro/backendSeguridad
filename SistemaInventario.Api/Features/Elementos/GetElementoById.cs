using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

// --- DTOs (Request / Response) ---
public class GetElementoByIdRequest { }
public class GetElementoByIdResponse
{
    public Guid Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? RutaImagen { get; set; }
    public Guid UsuarioIdPropietario { get; set; }
}

// --- Endpoint / Controlador ---
public static class GetElementoByIdEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/elementos/{id}", (string id, GetElementoByIdHandler handler) => handler.HandleAsync(id))
            .RequireAuthorization()
            .WithTags("Elementos")
            .WithSummary("Obtener un elemento por su identificador")
            .WithDescription("Recupera un elemento específico del inventario.")
            .Produces<GetElementoByIdResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}

// --- Lógica de Negocio (Handler) ---
public class GetElementoByIdHandler
{
    private readonly ApplicationDbContext _db;

    public GetElementoByIdHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync(string id)
    {
        if (!Guid.TryParse(id, out var elementoId))
            return Results.BadRequest("Id inválido.");

        var elemento = await _db.Elementos
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == elementoId);

        if (elemento == null)
            return Results.NotFound();

        return Results.Ok(new GetElementoByIdResponse
        {
            Id = elemento.Id,
            CodigoBarras = elemento.CodigoBarras,
            Nombre = elemento.Nombre,
            Descripcion = elemento.Descripcion,
            Categoria = elemento.Categoria,
            Precio = elemento.Precio,
            RutaImagen = elemento.RutaImagen,
            UsuarioIdPropietario = elemento.UsuarioIdPropietario
        });
    }
}
