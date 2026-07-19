using SistemaInventario.Api.Domain.Entities;

namespace SistemaInventario.Api.Infrastructure.Security;

public interface IJwtProvider
{
    string GenerateToken(Usuario usuario);
}
