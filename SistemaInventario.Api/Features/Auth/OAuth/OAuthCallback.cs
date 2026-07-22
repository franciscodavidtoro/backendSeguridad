using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SistemaInventario.Api.Domain.Entities;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth.OAuth;

// --- DTOs internos (respuesta del endpoint de token de Google) ---
internal class GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

// --- Endpoint / Controlador ---
public static class OAuthCallbackEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/oauth/google/callback", HandleAsync)
            .AllowAnonymous()
            .WithTags("Autenticación y Cuentas - OAuth")
            .WithSummary("Callback de OAuth 2.0 de Google")
            .WithDescription("Google redirige aquí tras el consentimiento del usuario. Intercambia el 'code' por tokens, valida el id_token de Google, vincula o crea el Usuario correspondiente y redirige de vuelta al frontend con el JWT propio del sistema (o con un challengeToken si la cuenta tiene MFA activo).");
    }

    // Serializa la sección "buscar o crear Usuario" dentro de esta instancia del backend, por el
    // mismo motivo documentado en RegistroHandler: el proveedor InMemory usado en Development no
    // hace cumplir restricciones de unicidad entre DbContext concurrentes, y dos callbacks casi
    // simultáneos para el mismo email nuevo podrían crear cuentas duplicadas sin este lock.
    private static readonly object _bloqueoOAuth = new();

    private static async Task<IResult> HandleAsync(
        string? code,
        string? state,
        string? error,
        HttpContext httpContext,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ConfigurationManager<OpenIdConnectConfiguration> googleConfigManager,
        ApplicationDbContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("OAuthCallback");
        var frontendUrl = configuration["OAuth:FrontendRedirectUrl"] ?? "/";

        // 0. El usuario canceló el consentimiento o Google reportó un error explícito.
        if (!string.IsNullOrEmpty(error))
        {
            return Results.Redirect(RedirigirConError(frontendUrl, "acceso_denegado"));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Results.Redirect(RedirigirConError(frontendUrl, "solicitud_invalida"));
        }

        // 1. Validar el "state": debe ser un JWT emitido por esta misma API (protección CSRF).
        if (!ValidarState(state, configuration))
        {
            return Results.Redirect(RedirigirConError(frontendUrl, "estado_invalido"));
        }

        var clientId = configuration["OAuth:Google:ClientId"];
        var clientSecret = configuration["OAuth:Google:ClientSecret"];
        var redirectUri = configuration["OAuth:Google:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return Results.Redirect(RedirigirConError(frontendUrl, "oauth_no_configurado"));
        }

        try
        {
            // 2. Intercambiar el "code" de autorización por tokens (llamada servidor-a-servidor;
            // el client_secret nunca sale del backend).
            var httpClient = httpClientFactory.CreateClient("GoogleOAuth");
            var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                }));

            var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>();

            if (!tokenResponse.IsSuccessStatusCode || tokenPayload?.IdToken == null)
            {
                logger.LogWarning("Fallo el intercambio de código OAuth con Google: {Error} - {Descripcion}",
                    tokenPayload?.Error, tokenPayload?.ErrorDescription);
                return Results.Redirect(RedirigirConError(frontendUrl, "intercambio_fallido"));
            }

            // 3. Validar el id_token de Google contra sus claves públicas (JWKS), verificando
            // firma, emisor, audiencia (nuestro client_id) y expiración.
            var googleConfig = await googleConfigManager.GetConfigurationAsync(httpContext.RequestAborted);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = googleConfig.SigningKeys,
                ValidateIssuer = true,
                ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(60)
            };

            ClaimsPrincipal googlePrincipal;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                // Por defecto, JwtSecurityTokenHandler remapea silenciosamente claims cortos
                // estándar del JWT (ej. "sub" -> nameidentifier, "email" -> emailaddress) a URIs
                // largas de WS-Federation. Sin este Clear(), FindFirst("sub")/FindFirst("email")
                // siempre devuelven null para el id_token de Google, aunque el claim sí llegó.
                handler.InboundClaimTypeMap.Clear();
                googlePrincipal = handler.ValidateToken(tokenPayload.IdToken, validationParameters, out _);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "id_token de Google inválido en el callback OAuth.");
                return Results.Redirect(RedirigirConError(frontendUrl, "token_invalido"));
            }

            var emailVerificado = googlePrincipal.FindFirst("email_verified")?.Value;
            var email = googlePrincipal.FindFirst("email")?.Value;
            var googleSub = googlePrincipal.FindFirst("sub")?.Value;
            var nombre = googlePrincipal.FindFirst("name")?.Value;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(googleSub) ||
                !string.Equals(emailVerificado, "true", StringComparison.OrdinalIgnoreCase))
            {
                // Un email no verificado por Google no es una identidad confiable para
                // crear/vincular una cuenta (podría no pertenecerle a quien inició el flujo).
                return Results.Redirect(RedirigirConError(frontendUrl, "email_no_verificado"));
            }

            // 4. Vincular con un Usuario existente o crear uno nuevo.
            Usuario? usuario;
            lock (_bloqueoOAuth)
            {
                usuario = context.Usuarios.FirstOrDefault(u => u.Email == email);
                if (usuario == null)
                {
                    usuario = context.Usuarios.FirstOrDefault(u => u.OAuthProvider == "Google" && u.OAuthProviderId == googleSub);
                }

                if (usuario == null)
                {
                    bool esPrimerUsuario = !context.Usuarios.Any();

                    usuario = new Usuario
                    {
                        Id = Guid.NewGuid(),
                        Cedula = null,
                        Nombre = string.IsNullOrWhiteSpace(nombre) ? email : nombre,
                        Email = email,
                        // Las cuentas OAuth no tienen contraseña local. Se hashea un valor
                        // aleatorio (nunca conocido por nadie) para que un intento de login por
                        // password contra este email falle limpiamente con 401 en vez de lanzar
                        // una excepción de BCrypt por un hash vacío/mal formado.
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                        Rol = esPrimerUsuario ? "Admin" : "User",
                        OAuthProvider = "Google",
                        OAuthProviderId = googleSub
                    };
                    context.Usuarios.Add(usuario);
                }
                else if (usuario.OAuthProvider == null)
                {
                    // Cuenta que ya existía por email+password: se vincula con Google sin tocar
                    // su contraseña ni su rol actuales.
                    usuario.OAuthProvider = "Google";
                    usuario.OAuthProviderId = googleSub;
                }

                context.SaveChanges();
            }

            // 5. Si la cuenta tiene MFA activo, no se emite el JWT final: se sigue el mismo
            // flujo de challengeToken que usa el login normal (Login.cs), para no crear un
            // bypass del segundo factor a través de Google.
            var secretKey = configuration["JwtSettings:SecretKey"]!;
            var issuer = configuration["JwtSettings:Issuer"];
            var audience = configuration["JwtSettings:Audience"];

            if (usuario.MfaEnabled)
            {
                var challengeToken = GenerarChallengeTokenMfa(usuario.Id, secretKey, issuer, audience);
                return Results.Redirect($"{frontendUrl}#requiereMfa=true&challengeToken={Uri.EscapeDataString(challengeToken)}");
            }

            var jwtFinal = GenerarTokenCompleto(usuario, secretKey, issuer, audience);

            // Se usa el fragmento de URL (#) y no query string (?) para que el token no viaje en
            // el header Referer ni quede en logs de acceso del servidor si el frontend hace
            // alguna navegación posterior; solo es visible en el propio navegador del usuario.
            return Results.Redirect($"{frontendUrl}#token={Uri.EscapeDataString(jwtFinal)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado procesando el callback de OAuth con Google.");
            return Results.Redirect(RedirigirConError(frontendUrl, "fallo_oauth"));
        }
    }

    private static string RedirigirConError(string frontendUrl, string codigo) => $"{frontendUrl}#error={codigo}";

    private static bool ValidarState(string state, IConfiguration configuration)
    {
        var secretKey = configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrEmpty(secretKey)) return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = !string.IsNullOrEmpty(configuration["JwtSettings:Issuer"]),
                ValidIssuer = configuration["JwtSettings:Issuer"],
                ValidateAudience = !string.IsNullOrEmpty(configuration["JwtSettings:Audience"]),
                ValidAudience = configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(state, validationParameters, out _);
            return principal.FindFirst("oauth_csrf")?.Value == "true";
        }
        catch
        {
            return false;
        }
    }

    private static string GenerarTokenCompleto(Usuario usuario, string secretKey, string? issuer, string? audience)
    {
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
        return tokenHandler.WriteToken(token);
    }

    private static string GenerarChallengeTokenMfa(Guid usuarioId, string secretKey, string? issuer, string? audience)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
                new Claim("mfa_challenge", "true")
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
