using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth.Mfa;

// --- DTOs (Request / Response) ---
public class MfaLoginVerificarRequest
{
    /// <summary>Token de desafío devuelto por /api/auth/login cuando la cuenta tiene MFA activo.</summary>
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>Código de 6 dígitos generado por Google Authenticator.</summary>
    public string Codigo { get; set; } = string.Empty;
}

public class MfaLoginVerificarResponse
{
    public string Token { get; set; } = string.Empty;
}

// --- Endpoint / Controlador ---
public static class MfaLoginVerificarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/mfa/login-verificar", HandleAsync)
            .AllowAnonymous()
            .WithTags("Autenticación y Cuentas - MFA")
            .WithSummary("Canjear el challengeToken de login + código TOTP por el token JWT final")
            .WithDescription("Segundo paso del login para cuentas con MFA activo. Recibe el 'challengeToken' emitido por /api/auth/login (válido 5 minutos, no autentica ningún otro endpoint) junto al código de 6 dígitos vigente, y devuelve el token JWT completo si ambos son correctos.");
    }

    private static async Task<IResult> HandleAsync(MfaLoginVerificarRequest request, ApplicationDbContext context, IConfiguration configuration)
    {
        var secretKey = configuration["JwtSettings:SecretKey"];
        var issuer = configuration["JwtSettings:Issuer"];
        var audience = configuration["JwtSettings:Audience"];

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("La clave secreta del JWT no está configurada.");
        }

        if (string.IsNullOrWhiteSpace(request.ChallengeToken) || string.IsNullOrWhiteSpace(request.Codigo))
        {
            return Results.Unauthorized();
        }

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = !string.IsNullOrEmpty(issuer),
                ValidIssuer = issuer,
                ValidateAudience = !string.IsNullOrEmpty(audience),
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            principal = handler.ValidateToken(request.ChallengeToken, validationParameters, out _);
        }
        catch
        {
            // Challenge token inválido, expirado o manipulado.
            return Results.Unauthorized();
        }

        // JwtSecurityTokenHandler remapea automáticamente el claim corto "sub" a
        // ClaimTypes.NameIdentifier al validar el token (mismo comportamiento que
        // JwtValidationMiddleware), por eso se busca por NameIdentifier y no por "sub".
        var esChallengeMfa = principal.FindFirst("mfa_challenge")?.Value == "true";
        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!esChallengeMfa || !Guid.TryParse(subClaim, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var usuario = await context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null || !usuario.MfaEnabled || string.IsNullOrEmpty(usuario.MfaSecretKey))
        {
            return Results.Unauthorized();
        }

        var totp = new Totp(Base32Encoding.ToBytes(usuario.MfaSecretKey));
        var esValido = totp.VerifyTotp(request.Codigo.Trim(), out _, new VerificationWindow(previous: 1, future: 1));

        if (!esValido)
        {
            return Results.Json(new { mensaje = "Código MFA incorrecto." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // Segundo factor validado: ahora sí emitimos el token JWT completo, igual que un login sin MFA.
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(ClaimTypes.Role, usuario.Rol),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email)
            }),
            Expires = DateTime.UtcNow.AddHours(8),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Results.Ok(new MfaLoginVerificarResponse { Token = tokenString });
    }
}
