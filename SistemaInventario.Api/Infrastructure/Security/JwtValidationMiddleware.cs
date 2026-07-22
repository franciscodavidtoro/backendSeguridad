using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;

namespace SistemaInventario.Api.Infrastructure.Security;

public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public JwtValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader[7..].Trim();
            try
            {
                var secret = _configuration["JwtSettings:SecretKey"] ?? string.Empty;
                var issuer = _configuration["JwtSettings:Issuer"] ?? string.Empty;
                var audience = _configuration["JwtSettings:Audience"] ?? string.Empty;

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = !string.IsNullOrEmpty(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(60)
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

                // Los tokens de desafío MFA (emitidos tras validar la contraseña pero antes del
                // segundo factor) NUNCA deben autenticar endpoints protegidos: solo son válidos
                // en /api/auth/mfa/login-verificar, que los valida manualmente desde el body.
                var esChallengeMfa = principal?.FindFirst("mfa_challenge")?.Value == "true";

                if (!esChallengeMfa && principal?.Identity != null && principal.Identity.IsAuthenticated)
                {
                    context.User = principal;
                }
            }
            catch
            {
                // Token inválido: no establecer principal. Dejar que los endpoints autorizados respondan 401/403.
            }
        }

        await _next(context);
    }
}
