using System;

namespace SistemaInventario.Api.Domain.Entities;

using System.ComponentModel.DataAnnotations;
public class Usuario
{
    public Guid Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    [AllowedValues("User", "Admin", ErrorMessage = "Rol no válido.")]
    public string Rol { get; set; } = "User";
}
