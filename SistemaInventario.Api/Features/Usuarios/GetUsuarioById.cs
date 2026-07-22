using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Usuarios;

/// <summary>
/// DTOs para operación GetUsuarioById - Obtener detalles de un usuario específico
/// </summary>
public class GetUsuarioByIdRequest { }

public class GetUsuarioByIdResponse
{
    public Guid Id { get; set; }
    public string? Cedula { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
}

/// <summary>
/// Handler - Lógica de negocio para obtener usuario por ID
/// Responsabilidad: Buscar usuario específico, validar existencia, mapear a DTO
/// </summary>
public class GetUsuarioByIdHandler
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _usuarioId;

    public GetUsuarioByIdHandler(ApplicationDbContext context, Guid usuarioId)
    {
        _context = context;
        _usuarioId = usuarioId;
    }

    public async Task<IResult> Handle()
    {
        try
        {
            // Consultar usuario por ID
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _usuarioId);

            // Si no existe, retornar 404 Not Found
            if (usuario == null)
            {
                return Results.NotFound(new
                {
                    message = $"Usuario con ID {_usuarioId} no encontrado."
                });
            }

            // Mapear a DTO (excluyendo PasswordHash por seguridad)
            var response = new GetUsuarioByIdResponse
            {
                Id = usuario.Id,
                Cedula = usuario.Cedula,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Rol = usuario.Rol,
                MfaEnabled = usuario.MfaEnabled
            };

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
/// Endpoint - Mapeo HTTP GET /api/usuarios/{id}
/// Responsabilidad: Recibir solicitud HTTP con {id}, invocar Handler, retornar respuesta
/// Autorización: Requiere JWT válido (lectura global permitida)
/// </summary>
public static class GetUsuarioByIdEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/usuarios/{id:guid}", GetUsuarioByIdAsync)
            .WithName("GetUsuarioById")
            .WithTags("Usuarios")
            .WithOpenApi()
            .Produces<GetUsuarioByIdResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization() // Requiere JWT válido
            .WithSummary("Obtener detalles de un usuario específico")
            .WithDescription("""
                Retorna la información detallada de un usuario por su ID (UUID).
                Acceso: Universal para usuarios autenticados (cualquier rol).
                """);
    }

    private static async Task<IResult> GetUsuarioByIdAsync(Guid id, ApplicationDbContext context)
    {
        var handler = new GetUsuarioByIdHandler(context, id);
        return await handler.Handle();
    }
}
