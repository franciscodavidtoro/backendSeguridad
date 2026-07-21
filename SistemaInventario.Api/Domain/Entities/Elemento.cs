using System;

namespace SistemaInventario.Api.Domain.Entities;

public class Elemento
{
    public Guid Id { get; set; }
    public string CodigoBien { get; set; } = string.Empty;
    public string NombreBien { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Modelo { get; set; }
    public string? MarcaRazaOtros { get; set; }
    public string? Ubicacion { get; set; }
    public string? RutaImagen { get; set; }
    public Guid UsuarioIdPropietario { get; set; }
}
