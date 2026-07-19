using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading.Tasks;
using SistemaInventario.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace SistemaInventario.Api.Features.Revisiones;

public static class FinalizarRevision
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/revisiones/{id}/finalizar", HandleAsync)
            .RequireAuthorization()
            .WithTags("Procesos de Revisión y Auditoría")
            .WithSummary("Clausurar definitivamente una sesión de revisión activa")
            .WithDescription("Calcula la cobertura física y bloquea la sesión.")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);
    }

    public record Response(string Id, string Estado, int ElementosFaltantes);

    private static async Task<IResult> HandleAsync(string id, ApplicationDbContext db, HttpContext http)
    {
        // 1. Extraer JWT UUID y rol, validar propiedad u rol Admin
        var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var roleClaim = http.User.FindFirst(ClaimTypes.Role);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
            return Results.Forbid();

        var userRole = roleClaim?.Value ?? string.Empty;

        // 2. Parsear 'id' a Guid
        if (!Guid.TryParse(id, out var revisionId))
            return Results.BadRequest("Id de revisión inválido");

        // 3. Obtener la revisión
        var revision = await db.Revisiones.FindAsync(revisionId);
        if (revision == null)
            return Results.NotFound("Revisión no encontrada");

        // Verificar permiso: debe ser owner o Admin
        if (revision.UsuarioId != usuarioId && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        // 4. Comparar conteo de escaneos vs total de inventario
        var totalElementos = await db.Elementos.CountAsync();
        var escaneados = await db.RevisionDetalles.CountAsync(rd => rd.RevisionId == revisionId);
        var faltantes = Math.Max(0, totalElementos - escaneados);

        // 5. Cambiar estado y fecha fin
        revision.Estado = faltantes == 0 ? "Completada" : "Incompleta";
        revision.FechaFin = DateTime.UtcNow;

        db.Revisiones.Update(revision);
        await db.SaveChangesAsync();

        return Results.Ok(new Response(id, revision.Estado, faltantes));
    }
}