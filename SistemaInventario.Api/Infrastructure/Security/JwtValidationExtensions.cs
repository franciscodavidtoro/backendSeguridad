using Microsoft.AspNetCore.Builder;

namespace SistemaInventario.Api.Infrastructure.Security;

public static class JwtValidationExtensions
{
    public static IApplicationBuilder UseJwtValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtValidationMiddleware>();
    }
}
