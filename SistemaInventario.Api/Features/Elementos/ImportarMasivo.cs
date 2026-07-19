using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using SistemaInventario.Api.Domain.Entities;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Elementos;

public class ImportarMasivoRequest
{
    public IFormFile Archivo { get; set; } = default!;
}

public class ImportarMasivoResponse
{
    public int Procesados { get; set; }
    public int Ignorados { get; set; }
}

public static class ImportarMasivoEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/elementos/importar", async (HttpRequest request, ImportarMasivoHandler handler, HttpContext http) => await handler.HandleAsync(request, http))
            .RequireAuthorization()
            .WithTags("Procesamiento Masivo")
            .WithSummary("Importar elementos desde un archivo Excel o CSV")
            .WithDescription("Procesa un archivo subido y crea elementos asociados al usuario autenticado.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportarMasivoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}

public class ImportarMasivoHandler
{
    private readonly ApplicationDbContext _db;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    public ImportarMasivoHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IResult> HandleAsync(HttpRequest request, HttpContext http)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest("El contenido debe ser multipart/form-data.");

        var form = await request.ReadFormAsync();
        var archivo = form.Files.GetFile("archivo");
        if (archivo == null)
            return Results.BadRequest("Se requiere un archivo con el nombre 'archivo'.");

        if (archivo.Length <= 0 || archivo.Length > MaxFileSizeBytes)
            return Results.BadRequest("El archivo es demasiado grande o está vacío.");

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Results.BadRequest("El archivo debe ser .xlsx o .csv.");

        var usuarioId = GetLoggedUserId(http);
        if (usuarioId == null)
            return Results.Forbid();

        var rows = new List<ImportarFila>();
        using (var stream = archivo.OpenReadStream())
        {
            var excelType = extension == ".csv" ? ExcelType.CSV : ExcelType.XLSX;
            var importRows = await MiniExcel.QueryAsync<ImportarFila>(stream, string.Empty, excelType, "A1", null, CancellationToken.None, true);
            foreach (var row in importRows)
            {
                if (string.IsNullOrWhiteSpace(row.CodigoBarras) || string.IsNullOrWhiteSpace(row.Nombre) || string.IsNullOrWhiteSpace(row.Categoria))
                    continue;

                if (row.Precio < 0)
                    continue;

                rows.Add(row);
            }
        }

        if (!rows.Any())
            return Results.BadRequest("No se encontraron filas válidas en el archivo.");

        var duplicateCodes = rows.GroupBy(r => r.CodigoBarras.Trim()).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateCodes.Any())
            return Results.BadRequest($"El archivo contiene códigos de barras duplicados: {string.Join(", ", duplicateCodes)}.");

        var normalizados = rows.Select(r => new ImportarFila
        {
            CodigoBarras = r.CodigoBarras.Trim(),
            Nombre = r.Nombre.Trim(),
            Descripcion = r.Descripcion?.Trim(),
            Categoria = r.Categoria.Trim(),
            Precio = r.Precio
        }).ToList();

        var existentes = await _db.Elementos
            .Where(e => normalizados.Select(r => r.CodigoBarras).Contains(e.CodigoBarras))
            .Select(e => e.CodigoBarras)
            .ToListAsync();

        var nuevos = normalizados
            .Where(r => !existentes.Contains(r.CodigoBarras))
            .Select(r => new Elemento
            {
                Id = Guid.NewGuid(),
                CodigoBarras = r.CodigoBarras,
                Nombre = r.Nombre,
                Descripcion = r.Descripcion,
                Categoria = r.Categoria,
                Precio = r.Precio,
                UsuarioIdPropietario = usuarioId.Value
            })
            .ToList();

        if (!nuevos.Any())
            return Results.BadRequest("Todos los códigos de barras ya existen en el inventario.");

        await _db.Elementos.AddRangeAsync(nuevos);
        await _db.SaveChangesAsync();

        return Results.Ok(new ImportarMasivoResponse
        {
            Procesados = nuevos.Count,
            Ignorados = rows.Count - nuevos.Count
        });
    }

    private Guid? GetLoggedUserId(HttpContext http)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (claim == null || !Guid.TryParse(claim.Value, out var usuarioId))
            return null;
        return usuarioId;
    }

    public class ImportarFila
    {
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
    }
}
