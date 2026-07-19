using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SistemaInventario.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace SistemaInventario.Api.Features.Revisiones;

public static class GetRevisionById
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Recibe el string encriptado desde la URL
        app.MapGet("/api/revisiones/{id}", HandleAsync)
            .RequireAuthorization()
            .WithTags("Procesos de Revisión y Auditoría")
            .WithSummary("Obtener el detalle individual de una revisión")
            .Produces<GetRevisiones.RevisionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(string id, ApplicationDbContext db, HttpContext http)
    {
        if (!Guid.TryParse(id, out var revisionId))
            return Results.BadRequest("Id de revisión inválido");

        var revision = await db.Revisiones.FindAsync(revisionId);
        if (revision == null)
            return Results.NotFound();

        // Validar owner o Admin
        var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var roleClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
            return Results.Forbid();
        var userRole = roleClaim?.Value ?? string.Empty;
        if (revision.UsuarioId != usuarioId && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        var response = new GetRevisiones.RevisionResponse(revision.Id.ToString(), revision.UsuarioId.ToString(), revision.Estado, revision.FechaInicio, revision.FechaFin);
        return Results.Ok(response);
    }
}