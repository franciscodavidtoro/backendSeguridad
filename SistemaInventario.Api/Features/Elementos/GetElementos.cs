using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

// --- DTOs (Request / Response) ---
public class GetElementosRequest { }
public class GetElementosResponse
{
    public Guid Id { get; set; }
    public string CodigoBien { get; set; } = string.Empty;
    public string NombreBien { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Modelo { get; set; }
    public string? MarcaRazaOtros { get; set; }
    public string? Ubicacion { get; set; }
    public string? RutaImagen { get; set; }
    public Guid UsuarioIdPropietario { get; set; }
}

// --- Endpoint / Controlador ---
public static class GetElementosEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/elementos", (GetElementosHandler handler) => handler.HandleAsync())
            .RequireAuthorization()
            .WithTags("Elementos")
            .WithSummary("Recuperar el catálogo completo de elementos")
            .WithDescription("Devuelve todos los elementos del inventario para usuarios autenticados.")
            .Produces<List<GetElementosResponse>>(StatusCodes.Status200OK);
    }
}

// --- Lógica de Negocio (Handler) ---
public class GetElementosHandler
{
    private readonly ApplicationDbContext _db;

    public GetElementosHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync()
    {
        var elementos = await _db.Elementos
            .AsNoTracking()
            .Select(e => new GetElementosResponse
            {
                Id = e.Id,
                CodigoBien = e.CodigoBien,
                NombreBien = e.NombreBien,
                Serie = e.Serie,
                Modelo = e.Modelo,
                MarcaRazaOtros = e.MarcaRazaOtros,
                Ubicacion = e.Ubicacion,
                RutaImagen = e.RutaImagen,
                UsuarioIdPropietario = e.UsuarioIdPropietario
            })
            .ToListAsync();

        return Results.Ok(elementos);
    }
}
