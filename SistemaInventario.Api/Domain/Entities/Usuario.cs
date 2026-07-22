using System;

namespace SistemaInventario.Api.Domain.Entities;

using System.ComponentModel.DataAnnotations;
public class Usuario
{
    public Guid Id { get; set; }

    /// <summary>
    /// Cédula ecuatoriana (Módulo 10). Obligatoria para cuentas registradas por
    /// email+contraseña. Queda nula para cuentas creadas vía OAuth (el proveedor externo no la
    /// entrega); el usuario puede completarla después con PUT /api/usuarios/{id}.
    /// </summary>
    public string? Cedula { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    [AllowedValues("User", "Admin", ErrorMessage = "Rol no válido.")]
    public string Rol { get; set; } = "User";

    /// <summary>
    /// Proveedor externo vinculado a esta cuenta (ej. "Google"), o nulo si es una cuenta
    /// exclusivamente local (email + contraseña).
    /// </summary>
    public string? OAuthProvider { get; set; }

    /// <summary>
    /// Identificador único ("sub") que el proveedor externo asigna a este usuario. Se usa para
    /// vincular la cuenta de forma estable, independiente de si el email cambia en el proveedor.
    /// </summary>
    public string? OAuthProviderId { get; set; }

    /// <summary>
    /// Secreto TOTP (Base32) usado por Google Authenticator. Nulo mientras el usuario no haya
    /// generado un secreto vía /api/auth/mfa/setup, o tras desactivar el MFA.
    /// </summary>
    public string? MfaSecretKey { get; set; }

    /// <summary>
    /// Indica si el segundo factor (TOTP) es exigido en el login. Solo pasa a true tras
    /// confirmar el secreto pendiente con /api/auth/mfa/activar.
    /// </summary>
    public bool MfaEnabled { get; set; } = false;
}
