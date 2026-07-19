using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaInventario.Api.Infrastructure.Database;

namespace SistemaInventario.Api.Features.Usuarios;

/// <summary>
/// DTOs para operación UpdateUsuario - Actualizar datos de un usuario existente
/// </summary>
public class UpdateUsuarioRequest
{
    public string? Nombre { get; set; }
    public string? Email { get; set; }
    public string? Cedula { get; set; }
    public string? Rol { get; set; }
}

public class UpdateUsuarioResponse
{
    public Guid Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}

/// <summary>
/// Handler - Lógica de negocio para actualizar usuario
/// Responsabilidad: Validar autorización, validar datos, actualizar en BD
/// </summary>
public class UpdateUsuarioHandler
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _usuarioId;
    private readonly Guid _usuarioAutenticadoId;
    private readonly string _rolAutenticado;
    private readonly UpdateUsuarioRequest _request;

    public UpdateUsuarioHandler(
        ApplicationDbContext context,
        Guid usuarioId,
        Guid usuarioAutenticadoId,
        string rolAutenticado,
        UpdateUsuarioRequest request)
    {
        _context = context;
        _usuarioId = usuarioId;
        _usuarioAutenticadoId = usuarioAutenticadoId;
        _rolAutenticado = rolAutenticado;
        _request = request;
    }

    public async Task<IResult> Handle()
    {
        try
        {
            // PASO 1: Validar autorización
            // Admin puede modificar cualquier usuario, User solo su propio perfil
            bool tienePermiso = _rolAutenticado == "Admin" || _usuarioAutenticadoId == _usuarioId;
            if (!tienePermiso)
            {
                return Results.Forbid();
            }

            // PASO 2: Buscar usuario a actualizar
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == _usuarioId);
            if (usuario == null)
            {
                return Results.NotFound(new
                {
                    message = $"Usuario con ID {_usuarioId} no encontrado."
                });
            }

            // PASO 3: Validar y actualizar campos
            var errores = new List<string>();

            // Validar Nombre si se proporciona
            if (!string.IsNullOrWhiteSpace(_request.Nombre))
            {
                if (_request.Nombre.Length < 2)
                    errores.Add("El nombre debe tener al menos 2 caracteres.");
                else
                    usuario.Nombre = _request.Nombre.Trim();
            }

            // Validar Email si se proporciona
            if (!string.IsNullOrWhiteSpace(_request.Email))
            {
                var emailTrimmed = _request.Email.Trim().ToLower();
                
                // Verificar formato válido
                if (!IsValidEmail(emailTrimmed))
                    errores.Add("El formato del email es inválido.");
                
                // Verificar unicidad (excepto para el usuario actual)
                var emailYaExiste = await _context.Usuarios
                    .AnyAsync(u => u.Email.ToLower() == emailTrimmed && u.Id != _usuarioId);
                if (emailYaExiste)
                {
                    return Results.Conflict(new
                    {
                        message = "El email ya se encuentra registrado en el sistema."
                    });
                }

                if (errores.Count == 0)
                    usuario.Email = emailTrimmed;
            }

            // Validar Cédula si se proporciona
            if (!string.IsNullOrWhiteSpace(_request.Cedula))
            {
                var cedulaTrimmed = _request.Cedula.Trim();

                // Validar formato (10 dígitos)
                if (!IsValidCedulaFormat(cedulaTrimmed))
                    errores.Add("La cédula debe contener exactamente 10 dígitos.");

                // Validar módulo 10 (Cédula Ecuatoriana)
                if (errores.Count == 0 && !ValidateCedulaModulo10(cedulaTrimmed))
                    errores.Add("La cédula no cumple con la validación de módulo 10.");

                // Verificar unicidad
                if (errores.Count == 0)
                {
                    var cedulaYaExiste = await _context.Usuarios
                        .AnyAsync(u => u.Cedula == cedulaTrimmed && u.Id != _usuarioId);
                    if (cedulaYaExiste)
                    {
                        return Results.Conflict(new
                        {
                            message = "La cédula ya se encuentra registrada en el sistema."
                        });
                    }
                    usuario.Cedula = cedulaTrimmed;
                }
            }

            // Validar Rol si se proporciona (solo Admin puede cambiar roles)
            if (!string.IsNullOrWhiteSpace(_request.Rol))
            {
                if (_rolAutenticado != "Admin")
                {
                    errores.Add("Solo los administradores pueden cambiar el rol de un usuario.");
                }
                else
                {
                    var rolTrimmed = _request.Rol.Trim();
                    if (rolTrimmed != "Admin" && rolTrimmed != "User")
                    {
                        errores.Add("El rol debe ser 'Admin' o 'User'.");
                    }
                    else
                    {
                        usuario.Rol = rolTrimmed;
                    }
                }
            }

            // PASO 4: Retornar errores si existen
            if (errores.Count > 0)
            {
                return Results.BadRequest(new
                {
                    message = "Validación fallida.",
                    errors = errores
                });
            }

            // PASO 5: Guardar cambios en BD
            await _context.SaveChangesAsync();

            // PASO 6: Retornar usuario actualizado
            var response = new UpdateUsuarioResponse
            {
                Id = usuario.Id,
                Cedula = usuario.Cedula,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Rol = usuario.Rol
            };

            return Results.Ok(response);
        }
        catch (Exception _)
        {
            // Retornar error genérico
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Valida que el email tenga un formato básico válido
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Valida que la cédula tenga exactamente 10 dígitos
    /// </summary>
    private static bool IsValidCedulaFormat(string cedula)
    {
        return cedula.Length == 10 && cedula.All(char.IsDigit);
    }

    /// <summary>
    /// Valida la cédula ecuatoriana usando el algoritmo de módulo 10
    /// Algoritmo descrito en la especificación técnica
    /// </summary>
    private static bool ValidateCedulaModulo10(string cedula)
    {
        if (!IsValidCedulaFormat(cedula))
            return false;

        // Multiplicadores para posiciones 0-6 (los primeros 7 dígitos)
        int[] multiplicadores = { 3, 2, 7, 6, 5, 4, 3 };
        int suma = 0;

        // Calcular suma de (dígito * multiplicador)
        for (int i = 0; i < 7; i++)
        {
            int digito = cedula[i] - '0';
            int producto = digito * multiplicadores[i];
            suma += producto;
        }

        // Calcular dígito verificador esperado
        int residuo = suma % 11;
        int digitoEsperado = 11 - residuo;
        if (digitoEsperado >= 10)
            digitoEsperado -= 11;

        // Comparar con el dígito verificador real (última posición, índice 9)
        int digitoReal = cedula[9] - '0';
        return digitoEsperado == digitoReal;
    }
}

/// <summary>
/// Endpoint - Mapeo HTTP PUT /api/usuarios/{id}
/// Responsabilidad: Recibir solicitud HTTP, extraer claims, invocar Handler
/// Autorización: JWT requerido (propietario del perfil o Admin)
/// </summary>
public static class UpdateUsuarioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/usuarios/{id:guid}", UpdateUsuarioAsync)
            .WithName("UpdateUsuario")
            .WithTags("Usuarios")
            .WithOpenApi()
            .Produces<UpdateUsuarioResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization() // Requiere JWT válido
            .WithSummary("Actualizar datos de un usuario")
            .WithDescription("""
                Permite actualizar los datos de un usuario.
                Solo el propietario del perfil o un administrador pueden hacer cambios.
                El rol solo puede ser modificado por administradores.
                """);
    }

    private static async Task<IResult> UpdateUsuarioAsync(
        Guid id,
        UpdateUsuarioRequest request,
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        // Extraer claims del JWT
        var usuarioIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var rolClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";

        // Validar que se pudo extraer el ID del usuario autenticado
        if (!Guid.TryParse(usuarioIdClaim, out var usuarioAutenticadoId))
        {
            return Results.Unauthorized();
        }

        var handler = new UpdateUsuarioHandler(context, id, usuarioAutenticadoId, rolClaim, request);
        return await handler.Handle();
    }
}
