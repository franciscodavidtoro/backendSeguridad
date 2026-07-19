using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Usuarios;

/// <summary>
/// DTOs para operación DeleteUsuario - Eliminar un usuario del sistema
/// </summary>
public class DeleteUsuarioRequest { }

public class DeleteUsuarioResponse { }

/// <summary>
/// Handler - Lógica de negocio para eliminar usuario
/// Responsabilidad: Validar autorización, eliminar usuario de BD
/// </summary>
public class DeleteUsuarioHandler
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _usuarioId;
    private readonly Guid _usuarioAutenticadoId;
    private readonly string _rolAutenticado;

    public DeleteUsuarioHandler(
        ApplicationDbContext context,
        Guid usuarioId,
        Guid usuarioAutenticadoId,
        string rolAutenticado)
    {
        _context = context;
        _usuarioId = usuarioId;
        _usuarioAutenticadoId = usuarioAutenticadoId;
        _rolAutenticado = rolAutenticado;
    }

    public async Task<IResult> Handle()
    {
        try
        {
            // PASO 1: Validar autorización
            // Admin puede eliminar cualquier usuario, User solo su propia cuenta
            bool tienePermiso = _rolAutenticado == "Admin" || _usuarioAutenticadoId == _usuarioId;
            if (!tienePermiso)
            {
                return Results.Forbid();
            }

            // PASO 2: Buscar usuario a eliminar
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == _usuarioId);
            if (usuario == null)
            {
                return Results.NotFound(new
                {
                    message = $"Usuario con ID {_usuarioId} no encontrado."
                });
            }

            // PASO 3: Eliminar usuario
            // NOTA: Elementos creados por este usuario quedarán huérfanos (UsuarioIdPropietario seguirá apuntando al ID eliminado)
            // Esto es por diseño: Mantener trazabilidad histórica de auditoría
            // Future: Considerar reasignación a cuenta administrativa o marcar como "Huérfano"
            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            // PASO 4: Retornar 204 No Content (estándar REST para DELETE exitoso)
            return Results.NoContent();
        }
        catch (Exception _)
        {
            // Retornar error genérico
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Endpoint - Mapeo HTTP DELETE /api/usuarios/{id}
/// Responsabilidad: Recibir solicitud HTTP, extraer claims, invocar Handler
/// Autorización: JWT requerido (propietario de la cuenta o Admin)
/// </summary>
public static class DeleteUsuarioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/usuarios/{id:guid}", DeleteUsuarioAsync)
            .WithName("DeleteUsuario")
            .WithTags("Usuarios")
            .WithOpenApi()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization() // Requiere JWT válido
            .WithSummary("Eliminar un usuario del sistema")
            .WithDescription("""
                Elimina permanentemente un usuario del sistema.
                Solo el propietario de la cuenta o un administrador pueden realizar esta acción.
                Los elementos creados por el usuario se mantienen en el inventario (quedarán huérfanos).
                """);
    }

    private static async Task<IResult> DeleteUsuarioAsync(
        Guid id,
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        // Extraer claims del JWT
        var usuarioIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var rolClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";

        // Validar que se pudo extraer el ID del usuario autenticado
        if (!Guid.TryParse(usuarioIdClaim, out var usuarioAutenticadoId))
        {
            return Results.Unauthorized();
        }

        var handler = new DeleteUsuarioHandler(context, id, usuarioAutenticadoId, rolClaim);
        return await handler.Handle();
    }
}
