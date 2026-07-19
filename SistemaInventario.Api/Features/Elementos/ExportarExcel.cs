using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

public class ExportarExcelRequest
{
    public string? Buscar { get; set; }
}

public class ExportarExcelResponse { }

public static class ExportarExcelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/elementos/exportar", async (HttpRequest request, ExportarExcelHandler handler) => await handler.HandleAsync(request))
            .RequireAuthorization()
            .WithTags("Procesamiento Masivo")
            .WithSummary("Exportar catálogo de elementos a archivo Excel")
            .WithDescription("Genera un archivo Excel con el inventario filtrado por nombre o código de barras.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}

public class ExportarExcelHandler
{
    private readonly ApplicationDbContext _db;

    public ExportarExcelHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync(HttpRequest request)
    {
        var buscar = request.Query["buscar"].ToString();

        var query = _db.Elementos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            query = query.Where(e => e.Nombre.Contains(term) || e.CodigoBarras.Contains(term));
        }

        var elementos = await query
            .Select(e => new
            {
                e.Id,
                e.CodigoBarras,
                e.Nombre,
                e.Descripcion,
                e.Categoria,
                e.Precio,
                e.RutaImagen,
                e.UsuarioIdPropietario
            })
            .ToListAsync();

        await using var stream = new MemoryStream();
        await MiniExcel.SaveAsAsync(stream, elementos, true, string.Empty, ExcelType.XLSX, null, CancellationToken.None);
        stream.Position = 0;

        return Results.File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "elementos.xlsx");
    }
}
