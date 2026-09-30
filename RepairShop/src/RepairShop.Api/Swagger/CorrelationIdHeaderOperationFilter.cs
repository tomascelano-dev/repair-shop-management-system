using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RepairShop.Api.Swagger;

public sealed class CorrelationIdHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // Optional request header
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RepairShop.Api.Common.CorrelationIdMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional correlation id. If not provided, the server generates one.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
