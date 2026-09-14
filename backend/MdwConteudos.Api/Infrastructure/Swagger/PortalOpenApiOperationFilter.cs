using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using MdwConteudos.Api.Modules.ApiPublica;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public sealed class PortalOpenApiOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? "").TrimStart('/').Split('?')[0];
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant() ?? "GET";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(method + " " + path)))[..12].ToLowerInvariant();
        operation.OperationId = method.ToLowerInvariant() + "_" +
            Regex.Replace(path.Trim('/'), "[^a-zA-Z0-9]+", "_").Trim('_') + "_" + digest;
        operation.Summary ??= context.MethodInfo.Name;
        operation.Description ??= $"{method} {path}.";

        var risk = ExternalEffect(path);
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-docs-safe-try"] = new JsonNodeExtension(JsonValue.Create(risk is null)!);
        operation.Extensions["x-docs-side-effects"] = new JsonNodeExtension(JsonValue.Create(
            risk ?? (method is "GET" or "HEAD" or "OPTIONS" ? "none" : "state-change"))!);
        if (risk is not null)
            operation.Description += "\n\nExecução pelo portal desabilitada: " + risk + ".";

        if (operation.Responses is not null)
            foreach (var code in new[] { "204", "205", "304" })
                if (operation.Responses.TryGetValue(code, out var response))
                    response.Content?.Clear();

        if (context.MethodInfo.DeclaringType == typeof(ApiPublicaController))
        {
            switch (context.MethodInfo.Name)
            {
                case nameof(ApiPublicaController.DownloadRelatorio):
                case nameof(ApiPublicaController.DownloadPainel):
                    Binary(operation, "application/octet-stream");
                    break;
                case nameof(ApiPublicaController.DownloadScript):
                    Binary(operation, "application/zip");
                    var response = operation.Responses!["200"];
                    response.Headers!["X-Script-Download-Suffix"] = new OpenApiHeader
                    {
                        Description = "Sufixo ASCII da versão ativa ou padrão.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    };
                    response.Headers["X-Script-Download-Source"] = new OpenApiHeader
                    {
                        Description = "Origem do pacote.",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Enum = [JsonValue.Create("SCRIPT_VERSOES"), JsonValue.Create("SCRIPTLAUDO")]
                        }
                    };
                    break;
                case nameof(ApiPublicaController.ScriptImagem):
                    Binary(operation, "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp", "application/octet-stream");
                    break;
            }
            if (context.MethodInfo.Name == nameof(ApiPublicaController.Ecodoppler))
            {
                var parameter = operation.Parameters?.FirstOrDefault(p => p.Name == "referencia");
                if (parameter?.Schema is OpenApiSchema schema)
                    schema.Default = JsonValue.Create(1);
            }
        }

        // This action reads Request.Form.Files, so its files are invisible to ApiExplorer.
        if (path.Equals("/api/web/relatorios/importar-lote", StringComparison.OrdinalIgnoreCase) &&
            operation.RequestBody is OpenApiRequestBody importBody)
        {
            var media = importBody.Content?.Values.FirstOrDefault();
            if (media?.Schema is OpenApiSchema form)
            {
                form.Properties ??= new Dictionary<string, IOpenApiSchema>();
                form.Properties["arquivos"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                    Description = "Arquivos de relatório enviados como partes repetidas com o nome arquivos."
                };
                importBody.Content = new Dictionary<string, OpenApiMediaType> { ["multipart/form-data"] = media };
            }
        }

        // Real ApiExplorer names are preserved (including underscore names and casing).
        if (operation.RequestBody is OpenApiRequestBody body &&
            context.ApiDescription.ParameterDescriptions.Any(p => p.Source == BindingSource.FormFile))
        {
            var media = body.Content?.Values.FirstOrDefault();
            if (media is not null)
                body.Content = new Dictionary<string, OpenApiMediaType> { ["multipart/form-data"] = media };
        }
    }

    private static string? ExternalEffect(string path)
    {
        var value = path.ToLowerInvariant();
        if (value.Contains("firebird")) return "firebird-admin";
        if (value.Contains("azure")) return "azure";
        if (value.Contains("/publicacao")) return "publication";
        if (value.Contains("/agente") || value.StartsWith("/api/voice") ||
            value.StartsWith("/api/conversions")) return "ai-or-external-service";
        if (value.Contains("email") || value.Contains("/forgot-password") ||
            value.Contains("/solicitar-aprovacao") || value.Contains("/aprovar/")) return "email-or-approval";
        return null;
    }

    private static void Binary(OpenApiOperation operation, params string[] mediaTypes)
    {
        var response = operation.Responses!["200"];
        response.Content?.Clear();
        // SwaggerResponse creates mutable OpenApiResponse instances.
        if (response is not OpenApiResponse concrete) return;
        concrete.Content = mediaTypes.ToDictionary(m => m, _ => new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
        });
        concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
        concrete.Headers["Content-Disposition"] = new OpenApiHeader
        {
            Description = "attachment; filename=nome-do-arquivo (nome definido pelo servidor)",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };
    }
}
