using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RepairShop.Api.Swagger;

public sealed class DefaultProblemDetailsResponsesOperationFilter : IOperationFilter
{
    private static readonly OpenApiSchema ProblemSchema = new() { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ProblemDetails) } };
    private static readonly OpenApiSchema ValidationProblemSchema = new() { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ValidationProblemDetails) } };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses ??= new OpenApiResponses();

        Add(operation, "400", "Bad Request", ValidationProblemSchema);
        Add(operation, "401", "Unauthorized", ProblemSchema);
        Add(operation, "403", "Forbidden", ProblemSchema);
        Add(operation, "404", "Not Found", ProblemSchema);
        Add(operation, "500", "Server Error", ProblemSchema);
    }

    private static void Add(OpenApiOperation operation, string code, string description, OpenApiSchema schema)
    {
        if (operation.Responses.ContainsKey(code)) return;

        operation.Responses[code] = new OpenApiResponse
        {
            Description = description,
            Content =
            {
                ["application/problem+json"] = new OpenApiMediaType { Schema = schema }
            }
        };
    }
}
