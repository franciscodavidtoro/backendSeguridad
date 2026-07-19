using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
//using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SistemaInventario.Api.Infrastructure.Security;

/// <summary>
/// Filtro de Swagger que automáticamente agrega la respuesta 401 Unauthorized
/// a todos los endpoints que requieren autenticación (tienen .RequireAuthorization()).
/// De esta forma no es necesario documentar manualmente el código 401 en cada endpoint.
/// </summary>
public class DocumentarUnauthorizedFiltro : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Detecta si el endpoint fue configurado con .RequireAuthorization()
        var requiereAutorizacion = context.ApiDescription.ActionDescriptor.EndpointMetadata
                                           .OfType<IAuthorizeData>().Any();

        if (requiereAutorizacion)
        {
            // Agregar respuesta 401 Unauthorized si no existe
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse
                {
                    Description = "No autorizado. Token JWT ausente, inválido o expirado."
                });
            }
        }
    }
}
