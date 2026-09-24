using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public class PartnerJwtOperationFilter : IOperationFilter
{
    public const string PartnerScheme = "PartnerJwt";
    public const string WebScheme = "WebJwt";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? "").TrimStart('/').Split('?')[0];
        var isPublicController = context.MethodInfo.DeclaringType == typeof(ApiPublicaController);
        if (path.StartsWith("/apiconteudos/v1/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Security = [];
            if (context.ApiDescription.HttpMethod?.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) == true ||
                path.Equals("/apiconteudos/v1/token", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/apiconteudos/v1/health", StringComparison.OrdinalIgnoreCase))
                return;

            operation.Security.Add(Requirement(PartnerScheme, context.Document));
            operation.Responses ??= new OpenApiResponses();
            if (isPublicController)
            {
                var schema = context.SchemaGenerator.GenerateSchema(typeof(ApiErrorDoc), context.SchemaRepository);
                operation.Responses["401"] = ErrorResponse("JWT de parceiro ausente ou inválido", schema);
                operation.Responses["503"] = ErrorResponse("Autenticação da API não configurada", schema);
            }
            return;
        }
        if (path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase) && isPublicController)
        {
            operation.Security = [];
            if (context.ApiDescription.HttpMethod?.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) == true
                || path.EndsWith("/token", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/health", StringComparison.OrdinalIgnoreCase)) return;
            operation.Security.Add(Requirement(PartnerScheme, context.Document));
            operation.Security.Add(Requirement(WebScheme, context.Document));
            operation.Responses ??= new OpenApiResponses();
            var schema = context.SchemaGenerator.GenerateSchema(typeof(ApiErrorDoc), context.SchemaRepository);
            operation.Responses["401"] = ErrorResponse("JWT de parceiro ou web ausente ou inválido", schema);
            operation.Responses["403"] = ErrorResponse("Usuário web sem permissão para a operação", schema);
            return;
        }
        if (!path.StartsWith("/api/web/", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/conversions", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/voice", StringComparison.OrdinalIgnoreCase)) return;

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata.Cast<object>()
            .Concat(context.MethodInfo.GetCustomAttributes(true))
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? []);
        var attributes = metadata.ToArray();
        operation.Security = [];
        if (attributes.OfType<IAllowAnonymous>().Any() || !attributes.OfType<IAuthorizeData>().Any()) return;
        operation.Security.Add(Requirement(WebScheme, context.Document));
        operation.Responses ??= new OpenApiResponses();
        // JWT bearer challenges/forbids have no JSON error envelope.
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "JWT de login web ausente ou inválido" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Usuário sem autorização para esta ação" });
    }

    private static OpenApiSecurityRequirement Requirement(string scheme, OpenApiDocument document) => new()
    {
        [new OpenApiSecuritySchemeReference(scheme, document)] = []
    };

    private static OpenApiResponse ErrorResponse(string description, IOpenApiSchema schema) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = schema } }
    };
}
