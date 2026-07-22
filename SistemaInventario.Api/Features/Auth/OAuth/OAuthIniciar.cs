using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SistemaInventario.Api.Features.Auth.OAuth;

// --- Endpoint / Controlador ---
public static class OAuthIniciarEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/oauth/google/iniciar", Handle)
            .AllowAnonymous()
            .WithTags("Autenticación y Cuentas - OAuth")
            .WithSummary("Iniciar el flujo de OAuth 2.0 con Google")
            .WithDescription("Redirige (302) al usuario a la pantalla de consentimiento de Google. El navegador debe navegar a esta URL directamente (no es una llamada XHR/fetch), ya que requiere interacción del usuario en un origen distinto.");
    }

    private static IResult Handle(IConfiguration configuration)
    {
        var clientId = configuration["OAuth:Google:ClientId"];
        var redirectUri = configuration["OAuth:Google:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return Results.Json(
                new { mensaje = "El login con Google no está configurado en este servidor (falta OAuth:Google:ClientId/RedirectUri)." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var state = GenerarStateFirmado(configuration);

        var authUrl = QueryHelpers.AddQueryString(
            "https://accounts.google.com/o/oauth2/v2/auth",
            new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["access_type"] = "online",
                ["prompt"] = "select_account"
            });

        return Results.Redirect(authUrl);
    }

    // El parámetro "state" del flujo OAuth es un JWT propio de corta duración (5 min), firmado
    // con la misma llave del resto del sistema. No requiere almacenamiento en el servidor (el
    // backend es horizontalmente escalable, sin estado compartido): el propio callback valida
    // la firma y expiración para confirmar que el "state" fue emitido por esta API y no fue
    // forjado por un atacante (protección CSRF del flujo de autorización).
    private static string GenerarStateFirmado(IConfiguration configuration)
    {
        var secretKey = configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("La clave secreta del JWT no está configurada.");
        var issuer = configuration["JwtSettings:Issuer"];
        var audience = configuration["JwtSettings:Audience"];

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("oauth_csrf", "true"),
                new Claim("proveedor", "google")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
