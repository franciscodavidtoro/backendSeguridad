using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading.Tasks;
using SistemaInventario.Api.Infrastructure.Database;
using SistemaInventario.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace SistemaInventario.Api.Features.Revisiones;

public static class CrearRevision
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/revisiones", HandleAsync)
            .RequireAuthorization() // Exige token JWT
            .WithTags("Procesos de Revisión y Auditoría")
            .WithSummary("Inicializar una sesión de auditoría física de inventario")
            .WithDescription("Crea una cabecera de auditoría en la tabla maestro con estado inicial 'EnCurso'.")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    // Devuelve el ID de la revisión (se encriptará antes de salir)
    public record Response(string Id);

    private static async Task<IResult> HandleAsync(HttpContext http, ApplicationDbContext db)
    {
        // 1. Extraer UsuarioId del JWT
        var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier) ?? http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var usuarioId))
            return Results.Forbid();

        // 2. Crear entidad Revision (Estado = EnCurso, FechaInicio = Now)
        var revision = new Revision
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Estado = "EnCurso",
            FechaInicio = DateTime.UtcNow
        };

        // 3. Guardar en EF Core
        await db.Revisiones.AddAsync(revision);
        await db.SaveChangesAsync();

        // 4. Retornar el Id generado (sin encriptar aquí)
        return Results.Created($"/api/revisiones/{revision.Id}", new Response(revision.Id.ToString()));
    }
}