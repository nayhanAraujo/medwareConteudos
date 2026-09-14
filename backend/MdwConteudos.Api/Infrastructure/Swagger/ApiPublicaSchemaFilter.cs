using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public sealed class ApiPublicaSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema generatedSchema, SchemaFilterContext context)
    {
        if (generatedSchema is not OpenApiSchema schema) return;
        if (context.Type == typeof(UpdateNormalidadeRequest))
        {
            schema.Required = new HashSet<string> { "valor_min", "valor_max" };
            foreach (var name in schema.Required)
                if (schema.Properties?[name] is OpenApiSchema property)
                    property.Type = JsonSchemaType.Number;
            return;
        }
        if (context.Type.Namespace != typeof(ApiErrorDoc).Namespace || schema.Properties is null)
            return;

        var nullability = new NullabilityInfoContext();
        schema.Required = new HashSet<string>();
        foreach (var property in context.Type.GetProperties())
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (name is null || !schema.Properties.TryGetValue(name, out var propertySchema)) continue;
            if (!property.IsDefined(typeof(DocOptionalAttribute)))
                schema.Required.Add(name);
            if (name is "timestamp" or "ultima_atualizacao" or "data_verificacao" or "dthrcriacao")
                if (propertySchema is OpenApiSchema date)
                    date.Description = "Data/hora ISO 8601. O contrato legado pode retornar hora local sem fuso; não é necessariamente RFC 3339.";
            var nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null ||
                           nullability.Create(property).ReadState == NullabilityState.Nullable;
            if (propertySchema is OpenApiSchema concrete)
            {
                if (nullable) concrete.Type |= JsonSchemaType.Null;
                else concrete.Type &= ~JsonSchemaType.Null;
            }
            else if (nullable)
            {
                // A nullable reference must not make the shared component nullable for all callers.
                schema.Properties[name] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object | JsonSchemaType.Null,
                    AllOf = [propertySchema]
                };
            }
        }
        if (context.Type == typeof(ClienteVariavelDoc))
        {
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = context.SchemaGenerator.GenerateSchema(
                typeof(Dictionary<string, NormalidadeFaixaDoc>), context.SchemaRepository);
        }
    }
}
