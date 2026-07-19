using System;

namespace SistemaInventario.Api.Domain.Entities;

public class RevisionDetalle
{
    public Guid Id { get; set; }
    public Guid RevisionId { get; set; }
    public Guid ElementoId { get; set; }
    public DateTime FechaEscaneo { get; set; }
}
