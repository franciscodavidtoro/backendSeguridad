using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaInventario.Api.Domain.Entities;

public class Revision
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
