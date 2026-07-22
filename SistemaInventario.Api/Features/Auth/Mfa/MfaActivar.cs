using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth.Mfa;

// --- DTOs (Request / Response) ---
public class MfaActivarRequest
{
    /// <summary>Código de 6 dígitos generado por Google Authenticator a partir del secreto entregado en /setup.</summary>
    public string Codigo { get; set; } = string.Empty;
}

public class MfaActivarResponse
{
    public string Mensaje { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class MfaActivarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/mfa/activar", HandleAsync)
            .RequireAuthorization()
            .WithTags("Autenticación y Cuentas - MFA")
            .WithSummary("Confirmar el secreto TOTP pendiente y activar el MFA")
            .WithDescription("Valida el primer código de 6 dígitos generado por Google Authenticator contra el secreto pendiente creado en /api/auth/mfa/setup. Si coincide, el MFA queda activo y todo login futuro de esta cuenta exigirá el segundo factor.");
    }

    private static async Task<IResult> HandleAsync(MfaActivarRequest request, HttpContext httpContext, ApplicationDbContext context)
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

        if (usuario.MfaEnabled)
        {
            return Results.Conflict(new MfaActivarResponse { Mensaje = "El MFA ya se encuentra activo." });
        }

        if (string.IsNullOrEmpty(usuario.MfaSecretKey))
        {
            return Results.BadRequest(new MfaActivarResponse { Mensaje = "No hay un secreto MFA pendiente. Genere uno primero mediante /api/auth/mfa/setup." });
        }

        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return Results.BadRequest(new MfaActivarResponse { Mensaje = "Debe proporcionar el código de 6 dígitos." });
        }

        var totp = new Totp(Base32Encoding.ToBytes(usuario.MfaSecretKey));
        var esValido = totp.VerifyTotp(request.Codigo.Trim(), out _, new VerificationWindow(previous: 1, future: 1));

        if (!esValido)
        {
            return Results.BadRequest(new MfaActivarResponse { Mensaje = "Código incorrecto. Verifique la hora de su dispositivo e inténtelo nuevamente." });
        }

        usuario.MfaEnabled = true;
        await context.SaveChangesAsync();

        return Results.Ok(new MfaActivarResponse { Mensaje = "MFA activado con éxito." });
    }
}
