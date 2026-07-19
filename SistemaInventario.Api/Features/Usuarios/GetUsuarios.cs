using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Usuarios;

/// <summary>
/// DTOs para operación GetUsuarios - Listar todos los usuarios del sistema
/// </summary>
public class GetUsuariosRequest { }

public class GetUsuariosResponse
{
    public Guid Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}

/// <summary>
/// Handler - Lógica de negocio para listar usuarios
/// Responsabilidad: Consultar todos los usuarios de la BD y mapearlos a DTOs
/// </summary>
public class GetUsuariosHandler
{
    private readonly ApplicationDbContext _context;

    public GetUsuariosHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle()
    {
        try
        {
            // Consultar todos los usuarios de forma asíncrona
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .ToListAsync();

            // Mapear a DTOs (excluyendo PasswordHash por seguridad)
            var response = usuarios.Select(u => new GetUsuariosResponse
            {
                Id = u.Id,
                Cedula = u.Cedula,
                Nombre = u.Nombre,
                Email = u.Email,
                Rol = u.Rol
            }).ToList();

            // Retornar con código 200 OK
            return Results.Ok(response);
        }
        catch (Exception _)
        {
            // Retornar error genérico sin exponer detalles internos
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Endpoint - Mapeo HTTP GET /api/usuarios
/// Responsabilidad: Recibir solicitud HTTP, invocar Handler, retornar respuesta
/// Autorización: Requiere JWT válido (cualquier rol autenticado)
/// </summary>
public static class GetUsuariosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/usuarios", GetUsuariosAsync)
            .WithName("GetUsuarios")
            .WithTags("Usuarios")
            .WithOpenApi()
            .Produces<List<GetUsuariosResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization() // Requiere JWT válido
            .WithSummary("Listar todos los usuarios del sistema")
            .WithDescription("""
                Retorna una lista completa de usuarios registrados.
                Acceso: Universal para usuarios autenticados (cualquier rol).
                """);
    }

    private static async Task<IResult> GetUsuariosAsync(ApplicationDbContext context)
    {
        var handler = new GetUsuariosHandler(context);
        return await handler.Handle();
    }
}
