using Microsoft.Extensions.Configuration;
using SistemaInventario.Api.Domain.Entities;

namespace SistemaInventario.Api.Infrastructure.Security;

public class MockJwtProvider : IJwtProvider
{
    private readonly IConfiguration _configuration;

    public MockJwtProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Usuario usuario)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"] ?? "default-secret-key";
        return $"{secretKey}-mock-token-{usuario.Id}";
    }
}
