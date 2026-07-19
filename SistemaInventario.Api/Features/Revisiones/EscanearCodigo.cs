using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;
using SistemaInventario.Api.Infrastructure.Database;
using SistemaInventario.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace SistemaInventario.Api.Features.Revisiones;

public static class EscanearCodigo
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/revisiones/{id}/escanear", HandleAsync)
            .RequireAuthorization()
            .WithTags("Procesos de Revisión y Auditoría")
            .WithSummary("Procesar el escaneo de un código de barras físico")
            .WithDescription("Verifica existencia del ítem e intercepta duplicados con 409 Conflict.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    public record EscaneoRequest(string CodigoBarras);

    private static async Task<IResult> HandleAsync(string id, EscaneoRequest request, ApplicationDbContext db, HttpContext http)
    {
        // 1. Intentar parsear 'id' como Guid
        if (!Guid.TryParse(id, out var revisionId))
            return Results.BadRequest("Id de revisión inválido");

        // 2. Verificar que la revisión exista y esté "EnCurso"
        var revision = await db.Revisiones.FindAsync(revisionId);
        if (revision == null || revision.Estado != "EnCurso")
            return Results.BadRequest("Revisión no encontrada o no está en curso");

        // 2.a Validar owner o rol Admin
        var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var roleClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
            return Results.Forbid();
        var userRole = roleClaim?.Value ?? string.Empty;
        if (revision.UsuarioId != usuarioId && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        // 3. Buscar CodigoBarras en maestro de Elementos
        var elemento = await db.Elementos.FirstOrDefaultAsync(e => e.CodigoBarras == request.CodigoBarras);
        if (elemento == null)
            return Results.NotFound("Elemento no encontrado con ese código de barras");

        // 4. Verificar duplicidad en RevisionDetalles
        var existe = await db.RevisionDetalles.AnyAsync(rd => rd.RevisionId == revisionId && rd.ElementoId == elemento.Id);
        if (existe)
            return Results.Conflict("El elemento ya fue escaneado en esta revisión");

        // 5. Insertar en RevisionDetalles
        var detalle = new RevisionDetalle
        {
            Id = Guid.NewGuid(),
            RevisionId = revisionId,
            ElementoId = elemento.Id,
            FechaEscaneo = DateTime.UtcNow
        };

        await db.RevisionDetalles.AddAsync(detalle);
        await db.SaveChangesAsync();

        return Results.Ok();
    }
}