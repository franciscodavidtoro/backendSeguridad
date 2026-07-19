using System;

namespace SistemaInventario.Api.Domain.Entities;

public class Elemento
{
    public Guid Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? RutaImagen { get; set; }
    public Guid UsuarioIdPropietario { get; set; }
}
