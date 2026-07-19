using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SistemaInventario.Api.Features.Imagenes;

// --- Reglas y utilidades compartidas por el CRUD de Imagenes ---
public static class ImagenReglas
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public static bool PuedeGestionar(Guid propietarioId, Guid usuarioActualId, string rol)
    {
        if (string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        return propietarioId == usuarioActualId;
    }

    public static Guid? GetLoggedUserId(HttpContext http)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (claim == null || !Guid.TryParse(claim.Value, out var usuarioId))
            return null;
        return usuarioId;
    }

    public static string GetLoggedUserRole(HttpContext http)
        => http.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
}

// Resuelve la carpeta física donde se guardan las imágenes, según FileStorage:ImagesPath.
public class ImagenStorage
{
    private readonly string _absolutePath;

    public ImagenStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var relativePath = configuration.GetValue<string>("FileStorage:ImagesPath")?.Trim() ?? "wwwroot/images/";
        _absolutePath = Path.GetFullPath(relativePath, environment.ContentRootPath);
        Directory.CreateDirectory(_absolutePath);
    }

    public string GetFullPath(string nombreArchivo) => Path.Combine(_absolutePath, nombreArchivo);

    public async Task<string> GuardarAsync(IFormFile archivo)
    {
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var nombreArchivo = $"{Guid.NewGuid()}{extension}";
        var destino = GetFullPath(nombreArchivo);

        await using var stream = new FileStream(destino, FileMode.Create);
        await archivo.CopyToAsync(stream);

        return nombreArchivo;
    }

    public void Eliminar(string nombreArchivo)
    {
        var ruta = GetFullPath(nombreArchivo);
        if (File.Exists(ruta))
            File.Delete(ruta);
    }
}
