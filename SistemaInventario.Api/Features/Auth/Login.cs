using System;
using System.Collections.Concurrent; // Necesario para el diccionario seguro en memoria
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SistemaInventario.Api.Domain.Entities;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Auth;

// --- DTOs (Request / Response) ---
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    /// <summary>Token JWT completo. Solo se emite si el usuario no tiene MFA activo.</summary>
    public string? Token { get; set; }

    /// <summary>true si la cuenta exige segundo factor; el cliente debe llamar a /api/auth/mfa/login-verificar.</summary>
    public bool RequiereMfa { get; set; } = false;

    /// <summary>Token de desafío de corta duración (5 min) que identifica la sesión pendiente de MFA. No sirve para autenticar ningún otro endpoint.</summary>
    public string? ChallengeToken { get; set; }
}

// --- Endpoint / Controlador ---
public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", (LoginRequest request, LoginHandler handler) =>
        {
            return handler.Handle(request);
        })
        .AllowAnonymous()
        .WithTags("Autenticación y Cuentas")
        .WithSummary("Autenticar credenciales de usuario y emitir token JWT")
        .WithDescription("Valida el correo y la contraseña. Cuenta intentos fallidos en memoria y bloquea temporalmente la cuenta por 5 minutos tras 5 intentos fallidos sin modificar la base de datos. Si la cuenta tiene MFA activo, no retorna el token final: retorna un 'challengeToken' de 5 minutos que debe canjearse en /api/auth/mfa/login-verificar junto al código de 6 dígitos de Google Authenticator.");
    }
}

// --- Lógica de Negocio (Handler) ---
public class LoginHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    // Estructura estática en memoria para rastrear intentos por Email: (Intentos, BloqueadoHasta)
    private static readonly ConcurrentDictionary<string, (int Intentos, DateTime? BloqueadoHasta)> _registroDeIntentos = new();

    public LoginHandler(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public IResult Handle(LoginRequest request)
    {
        string emailKey = request.Email.ToLower().Trim();

        // 1. Verificar si el correo ya está bloqueado temporalmente en memoria
        if (_registroDeIntentos.TryGetValue(emailKey, out var registro))
        {
            if (registro.BloqueadoHasta.HasValue && registro.BloqueadoHasta.Value > DateTime.UtcNow)
            {
                var tiempoRestante = registro.BloqueadoHasta.Value - DateTime.UtcNow;
                return Results.Json(new { Mensaje = $"Cuenta temporalmente bloqueada. Intente de nuevo en {Math.Ceiling(tiempoRestante.TotalMinutes)} minutos." }, statusCode: StatusCodes.Status429TooManyRequests);
            }
        }

        // 2. Buscar al usuario en la base de datos
        var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == request.Email);

        // 3. Validar credenciales
        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
        {
            // Incrementar contador de intentos fallidos
            int intentosActuales = 1;
            DateTime? bloqueadoHasta = null;

            if (_registroDeIntentos.TryGetValue(emailKey, out var registroExistente))
            {
                intentosActuales = registroExistente.Intentos + 1;
            }

            if (intentosActuales >= 5) // Umbral de bloqueo
            {
                bloqueadoHasta = DateTime.UtcNow.AddMinutes(5); // Bloqueo de 5 minutos
                _registroDeIntentos[emailKey] = (intentosActuales, bloqueadoHasta);

                return Results.Json(new { Mensaje = "Demasiados intentos fallidos. Cuenta bloqueada temporalmente por 5 minutos." }, statusCode: StatusCodes.Status429TooManyRequests);
            }
            else
            {
                _registroDeIntentos[emailKey] = (intentosActuales, null);
                return Results.Json(new { Mensaje = $"Credenciales incorrectas. Intentos fallidos: {intentosActuales}/5." }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        // 4. Si el login es exitoso, limpiamos su historial de intentos fallidos
        _registroDeIntentos.TryRemove(emailKey, out _);

        var secretKey = _configuration["JwtSettings:SecretKey"];
        var issuer = _configuration["JwtSettings:Issuer"];
        var audience = _configuration["JwtSettings:Audience"];

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("La clave secreta del JWT no está configurada.");
        }

        // 5. Si la cuenta exige MFA, no emitimos el token final todavía: solo un token de
        // desafío de corta duración que el cliente debe canjear en /api/auth/mfa/login-verificar
        // junto al código de 6 dígitos de Google Authenticator.
        if (usuario.MfaEnabled)
        {
            var challengeToken = GenerarChallengeTokenMfa(usuario.Id, secretKey, issuer, audience);
            return Results.Ok(new LoginResponse { RequiereMfa = true, ChallengeToken = challengeToken });
        }

        // 6. Generación del Token JWT completo (cuentas sin MFA activo)
        var tokenString = GenerarTokenCompleto(usuario, secretKey, issuer, audience);

        return Results.Ok(new LoginResponse { Token = tokenString });
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
            // Vida corta: solo el tiempo razonable para que el usuario abra Google Authenticator.
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}