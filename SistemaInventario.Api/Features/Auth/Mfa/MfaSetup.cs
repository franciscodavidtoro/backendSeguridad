using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using QRCoder;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth.Mfa;

// --- DTOs (Request / Response) ---
public class MfaSetupResponse
{
    /// <summary>Secreto Base32 en texto plano, por si el usuario prefiere ingresarlo manualmente en la app.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>URI otpauth:// que codifica el QR (útil para debug o apps que aceptan el link directo).</summary>
    public string OtpAuthUri { get; set; } = string.Empty;

    /// <summary>Imagen PNG del QR en Data URI, lista para insertarse en un &lt;img src="..."&gt; del frontend.</summary>
    public string QrCodeImagenBase64 { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class MfaSetupEndpoint
{
    private const string Emisor = "SistemaInventario";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/mfa/setup", HandleAsync)
            .RequireAuthorization()
            .WithTags("Autenticación y Cuentas - MFA")
            .WithSummary("Generar el secreto TOTP y el código QR para activar Google Authenticator")
            .WithDescription("Requiere sesión autenticada (JWT completo, sin MFA pendiente). Genera un secreto TOTP nuevo y lo deja en estado pendiente: el MFA no queda activo hasta confirmar el primer código en /api/auth/mfa/activar. Si ya existe un secreto pendiente, este endpoint lo reemplaza.");
    }

    private static async Task<IResult> HandleAsync(HttpContext httpContext, ApplicationDbContext context)
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
            return Results.Conflict(new { mensaje = "El MFA ya se encuentra activo para esta cuenta. Desactívelo antes de generar un nuevo secreto." });
        }

        // Secreto aleatorio de 160 bits (20 bytes), tamaño estándar recomendado para TOTP (RFC 4226/6238).
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretBase32 = Base32Encoding.ToString(secretBytes);

        usuario.MfaSecretKey = secretBase32;
        // MfaEnabled permanece en false: queda pendiente de confirmación vía /activar.
        await context.SaveChangesAsync();

        var otpAuthUri =
            $"otpauth://totp/{Uri.EscapeDataString(Emisor)}:{Uri.EscapeDataString(usuario.Email)}" +
            $"?secret={secretBase32}&issuer={Uri.EscapeDataString(Emisor)}&digits=6&period=30&algorithm=SHA1";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = pngQrCode.GetGraphic(20);
        var qrBase64 = Convert.ToBase64String(qrBytes);

        return Results.Ok(new MfaSetupResponse
        {
            SecretKey = secretBase32,
            OtpAuthUri = otpAuthUri,
            QrCodeImagenBase64 = $"data:image/png;base64,{qrBase64}"
        });
    }
}
