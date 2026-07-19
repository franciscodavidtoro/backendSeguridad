using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SistemaInventario.Api.Domain.Entities;

namespace SistemaInventario.Api.Infrastructure.Security;

public class JwtProvider : IJwtProvider
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtProvider(IConfiguration configuration)
    {
        _secretKey = configuration["JwtSettings:SecretKey"]?.Trim() ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
        _issuer = configuration["JwtSettings:Issuer"]?.Trim() ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        _audience = configuration["JwtSettings:Audience"]?.Trim() ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
    }

    public string GenerateToken(Usuario usuario)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim("nombre", usuario.Nombre)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
