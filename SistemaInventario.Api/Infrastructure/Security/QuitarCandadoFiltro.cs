using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SistemaInventario.Api.Infrastructure.Security;

public class QuitarCandadoFiltro : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Detecta si el endpoint fue configurado con .AllowAnonymous()
        var esAnonimo = context.ApiDescription.ActionDescriptor.EndpointMetadata
                               .OfType<IAllowAnonymous>().Any();

        if (esAnonimo)
        {
            // Al asignarle una lista vacía, sobreescribimos el requisito global
            // y Swagger retira el candado de la interfaz gráfica.
            operation.Security = new List<OpenApiSecurityRequirement>();
        }
    }
}