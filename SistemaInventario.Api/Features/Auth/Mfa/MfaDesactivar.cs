using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth.Mfa;

// --- DTOs (Request / Response) ---
public class MfaDesactivarRequest
{
    /// <summary>Código de 6 dígitos vigente, exigido como prueba de posesión del dispositivo antes de desactivar.</summary>
    public string Codigo { get; set; } = string.Empty;
}

public class MfaDesactivarResponse
{
    public string Mensaje { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class MfaDesactivarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/mfa/desactivar", HandleAsync)
            .RequireAuthorization()
            .WithTags("Autenticación y Cuentas - MFA")
            .WithSummary("Desactivar el MFA de la cuenta autenticada")
            .WithDescription("Exige un código TOTP vigente como prueba de posesión del dispositivo. Si es válido, desactiva el MFA y borra el secreto almacenado; el usuario deberá volver a ejecutar /setup + /activar si desea reactivarlo.");
    }

    private static async Task<IResult> HandleAsync(MfaDesactivarRequest request, HttpContext httpContext, ApplicationDbContext context)
    {
        var usuarioIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(usuarioIdClaim, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var usuario = await context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null)
        {
            return Results.NotFound();
        }

        if (!usuario.MfaEnabled || string.IsNullOrEmpty(usuario.MfaSecretKey))
        {
            return Results.BadRequest(new MfaDesactivarResponse { Mensaje = "El MFA no se encuentra activo en esta cuenta." });
        }

        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return Results.BadRequest(new MfaDesactivarResponse { Mensaje = "Debe proporcionar el código de 6 dígitos vigente." });
        }

        var totp = new Totp(Base32Encoding.ToBytes(usuario.MfaSecretKey));
        var esValido = totp.VerifyTotp(request.Codigo.Trim(), out _, new VerificationWindow(previous: 1, future: 1));

        if (!esValido)
        {
            return Results.BadRequest(new MfaDesactivarResponse { Mensaje = "Código incorrecto." });
        }

        usuario.MfaEnabled = false;
        usuario.MfaSecretKey = null;
        await context.SaveChangesAsync();

        return Results.Ok(new MfaDesactivarResponse { Mensaje = "MFA desactivado con éxito." });
    }
}
