using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SistemaInventario.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace SistemaInventario.Api.Features.Revisiones;

public static class GetRevisiones
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/revisiones", HandleAsync)
            .RequireAuthorization()
            .WithTags("Procesos de Revisión y Auditoría")
            .WithSummary("Recuperar la lista completa de sesiones de auditoría")
            .WithDescription("Obtiene un listado general del histórico y sesiones activas de revisiones.")
            .Produces<List<RevisionResponse>>(StatusCodes.Status200OK);
    }

    public record RevisionResponse(
        string Id, 
        string UsuarioId, 
        string Estado, 
        DateTime FechaInicio, 
        DateTime? FechaFin
    );

    private static async Task<IResult> HandleAsync(ApplicationDbContext db, HttpContext http)
    {
        // Extraer usuario y rol del token
        var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var roleClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
            return Results.Forbid();

        var userRole = roleClaim?.Value ?? string.Empty;

        // Si es Admin, devolver todas; si no, solo las del usuario
        var query = db.Revisiones.AsNoTracking().AsQueryable();
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => r.UsuarioId == usuarioId);

        var list = await query
            .Select(r => new RevisionResponse(r.Id.ToString(), r.UsuarioId.ToString(), r.Estado, r.FechaInicio, r.FechaFin))
            .ToListAsync();

        return Results.Ok(list);
    }
}