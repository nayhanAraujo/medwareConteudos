using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using MdwConteudos.Api.Infrastructure.Swagger;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.Swagger;

namespace ConversorHtml.Tests;

[CollectionDefinition("PortalOpenApi", DisableParallelization = true)]
public sealed class PortalOpenApiCollection;

[Collection("PortalOpenApi")]
public sealed class PortalOpenApiTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ISwaggerProvider _swagger;

    private sealed class DocumentationEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(ApiPublicaController).Assembly.GetName().Name!;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    public PortalOpenApiTests()
    {
        // No Program, app.Build/Start, hosted services, config files, or database factory.
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new DocumentationEnvironment());
        services.AddLogging();
        services.AddControllers().AddApplicationPart(typeof(ApiPublicaController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        services.AddPortalOpenApiMetadata();
        services.AddSwaggerGen(o =>
        {
            o.EnableAnnotations();
            foreach (var name in new[] { SwaggerDocPaths.Parceiros, SwaggerDocPaths.ApiInterna, SwaggerDocPaths.Web })
                o.SwaggerDoc(name, new OpenApiInfo { Title = name, Version = "1.0.0" });
            o.DocInclusionPredicate((name, api) => name switch
            {
                SwaggerDocPaths.Parceiros => api.RelativePath?.StartsWith("apiconteudos/v1/") == true,
                SwaggerDocPaths.ApiInterna => api.RelativePath?.StartsWith("api/v1/") == true ||
                    api.RelativePath?.StartsWith("api/conversions") == true || api.RelativePath?.StartsWith("api/voice") == true,
                SwaggerDocPaths.Web => api.RelativePath?.StartsWith("api/web/") == true,
                _ => false
            });
            o.AddPortalOpenApi();
        });
        _services = services.BuildServiceProvider();
        _swagger = _services.GetRequiredService<ISwaggerProvider>();
    }

    [Theory]
    [InlineData(SwaggerDocPaths.Parceiros)]
    [InlineData(SwaggerDocPaths.ApiInterna)]
    [InlineData(SwaggerDocPaths.Web)]
    public void Definitions_have_stable_unique_ids_and_serialize(string name)
    {
        var first = _swagger.GetSwagger(name);
        var second = _swagger.GetSwagger(name);
        var ids = Operations(first).Select(o => o.OperationId).ToArray();
        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids, Operations(second).Select(o => o.OperationId));
        var document = Json(first);
        Assert.Equal("1.0.0", document["info"]!["version"]!.GetValue<string>());
        Assert.All(Operations(first), o => Assert.NotNull(o.Extensions?["x-docs-safe-try"]));
    }

    [Fact]
    public void Public_definition_contains_exactly_22_operations_and_no_untyped_success()
    {
        var doc = _swagger.GetSwagger(SwaggerDocPaths.Parceiros);
        Assert.Equal(22, Operations(doc).Count());
        Assert.All(doc.Paths.Keys, p => Assert.StartsWith("/apiconteudos/v1/", p));
        Assert.All(Operations(doc), op =>
        {
            var response = op.Responses!["200"];
            Assert.NotEmpty(response.Content!);
            Assert.All(response.Content.Values, media => AssertTyped(media.Schema!, doc, new HashSet<string>()));
        });
    }

    [Fact]
    public void Every_public_json_example_matches_all_fields_types_required_and_nullability()
    {
        var doc = _swagger.GetSwagger(SwaggerDocPaths.Parceiros);
        foreach (var op in Operations(doc))
        foreach (var (code, response) in op.Responses!)
        foreach (var (mime, media) in response.Content ?? new Dictionary<string, OpenApiMediaType>())
        {
            if (mime != "application/json") continue;
            Assert.True(media.Example is not null, $"{op.OperationId} {code} is missing an example.");
            AssertExample(media.Example, media.Schema!, doc, $"{op.OperationId}/{code}");
        }
    }

    [Fact]
    public void Partner_and_web_security_are_separate_and_internal_aliases_require_either_token()
    {
        var partner = _swagger.GetSwagger(SwaggerDocPaths.Parceiros);
        var internalDoc = _swagger.GetSwagger(SwaggerDocPaths.ApiInterna);
        var web = _swagger.GetSwagger(SwaggerDocPaths.Web);
        Assert.Equal(new[] { "PartnerJwt" }, partner.Components!.SecuritySchemes!.Keys);
        Assert.Equal(new[] { "PartnerJwt", "WebJwt" }, internalDoc.Components!.SecuritySchemes!.Keys.Order());
        Assert.Equal(new[] { "WebJwt" }, web.Components!.SecuritySchemes!.Keys);
        foreach (var (path, item) in partner.Paths)
        foreach (var op in item.Operations!.Values)
        {
            if (path.EndsWith("/token") || path.EndsWith("/health")) Assert.Empty(op.Security!);
            else
            {
                Assert.Equal("PartnerJwt", Assert.Single(Assert.Single(op.Security!).Keys).Reference.Id);
                Assert.Contains("401", op.Responses!.Keys);
                Assert.Contains("503", op.Responses.Keys);
            }
        }
        foreach (var (path, item) in internalDoc.Paths.Where(p => p.Key.StartsWith("/api/v1/")))
        foreach (var op in item.Operations!.Values)
        {
            if (path.EndsWith("/token") || path.EndsWith("/health")) Assert.Empty(op.Security!);
            else
            {
                Assert.Equal(new[] { "PartnerJwt", "WebJwt" }, op.Security!.SelectMany(r => r.Keys).Select(k => k.Reference.Id).Order());
                Assert.Contains("401", op.Responses!.Keys);
                Assert.Contains("403", op.Responses.Keys);
            }
        }
        Assert.Empty(Get(web, "/api/web/auth/login", HttpMethod.Post).Security!);
        Assert.Empty(Get(web, "/api/web/auth/forgot-password", HttpMethod.Post).Security!);
        Assert.Equal("WebJwt", Assert.Single(Assert.Single(Get(web, "/api/web/auth/me").Security!).Keys).Reference.Id);
        Assert.Empty(Get(web, "/api/web/scripts/aprovar/{token}").Security!);
    }

    [Fact]
    public void Zip_images_downloads_and_204_are_not_json()
    {
        var doc = _swagger.GetSwagger(SwaggerDocPaths.Parceiros);
        var zip = Get(doc, "/apiconteudos/v1/scripts/{codscriptlaudo}/download").Responses!["200"];
        Assert.Equal("application/zip", Assert.Single(zip.Content!).Key);
        Assert.Equal("binary", zip.Content!["application/zip"].Schema!.Format);
        Assert.Contains("X-Script-Download-Source", zip.Headers!.Keys);
        Assert.Null(zip.Content["application/zip"].Example);
        var image = Get(doc, "/apiconteudos/v1/scripts/{codscriptlaudo}/imagem").Responses!["200"];
        Assert.Contains("image/png", image.Content!.Keys);
        Assert.Contains("image/jpeg", image.Content.Keys);
        Assert.DoesNotContain("application/json", image.Content.Keys);
        var empty = Get(doc, "/apiconteudos/v1/scripts/ultimo-verificado").Responses!["204"];
        Assert.True(empty.Content is null || empty.Content.Count == 0);
        Assert.False(Json(doc)["paths"]!["/apiconteudos/v1/scripts/ultimo-verificado"]!["get"]!["responses"]!["204"]!.AsObject().ContainsKey("content"));
    }

    [Fact]
    public void Web_file_uploads_preserve_form_names_and_binary_fields()
    {
        var doc = _swagger.GetSwagger(SwaggerDocPaths.Web);
        var form = Get(doc, "/api/web/referencias/{codReferencia}/anexos", HttpMethod.Post).RequestBody!;
        Assert.Equal("multipart/form-data", Assert.Single(form.Content!).Key);
        var schema = Resolve(form.Content["multipart/form-data"].Schema!, doc);
        Assert.Equal("binary", schema.Properties!["arquivo"].Format);
        Assert.Contains("descricao", schema.Properties.Keys);
        Assert.Contains("nome", schema.Properties.Keys);
        var import = Get(doc, "/api/web/relatorios/importar-lote", HttpMethod.Post).RequestBody!;
        var files = Resolve(import.Content!["multipart/form-data"].Schema!, doc).Properties!["arquivos"];
        Assert.Equal(JsonSchemaType.Array, files.Type);
        Assert.Equal("binary", files.Items!.Format);
        var descriptions = _services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(g => g.Items);
        var action = descriptions.First(d => d.RelativePath == "api/web/referencias/{codReferencia}/anexos" && d.HttpMethod == "POST");
        // ApiExplorer normalization must not change the actual MVC parameter's attribute.
        var parameter = action.ActionDescriptor.Parameters.Single(p => p.Name == "arquivo");
        var reflected = Assert.IsType<Microsoft.AspNetCore.Mvc.Controllers.ControllerParameterDescriptor>(parameter);
        Assert.NotNull(reflected.ParameterInfo.GetCustomAttribute<FromFormAttribute>());
    }

    [Theory]
    [InlineData("/api/web/auth/forgot-password", "POST")]
    [InlineData("/api/web/scripts/{id}/send-images-email", "POST")]
    [InlineData("/api/web/scripts/aprovar/{token}", "GET")]
    [InlineData("/api/web/assistente/publicacao", "GET")]
    [InlineData("/api/web/firebird-admin/sql", "POST")]
    public void External_effect_operations_are_marked_unsafe(string path, string method)
    {
        var op = Get(_swagger.GetSwagger(SwaggerDocPaths.Web), path, new HttpMethod(method));
        Assert.False(Assert.IsType<JsonNodeExtension>(op.Extensions!["x-docs-safe-try"]).Node!.GetValue<bool>());
    }

    [Fact]
    public void Domain_examples_and_optional_fields_are_faithful_to_legacy_contract()
    {
        var doc = _swagger.GetSwagger(SwaggerDocPaths.Parceiros);
        var info = ApiPublicaOpenApiExamples.SistemaInfoOk;
        Assert.False(info.AsObject().ContainsKey("total"));
        Assert.Equal("1.0", info["data"]!["versao_api"]!.GetValue<string>());
        Assert.Equal(7, info["data"]!["estatisticas"]!.AsObject().Count);
        Assert.NotNull(info["data"]!["variaveis_por_especialidade"]![0]!["especialidade"]);
        Assert.Equal("SCRIPT_VERSAO_MRD", ApiPublicaOpenApiExamples.ScriptsOk["data"]![0]!["mrd_fonte"]!.GetValue<string>());
        var script = doc.Components!.Schemas![nameof(ScriptItemDoc)];
        Assert.DoesNotContain("imagens", script.Required!);
        Assert.Contains("data_verificacao", script.Required!);
        Assert.True(script.Properties!["data_verificacao"].Type!.Value.HasFlag(JsonSchemaType.Null));
        var update = doc.Components.Schemas[typeof(UpdateNormalidadeRequest).FullName!];
        Assert.Equal(new[] { "valor_max", "valor_min" }, update.Required!.Order());
        Assert.Equal(JsonSchemaType.Number, update.Properties!["valor_min"].Type);
    }

    private static IEnumerable<OpenApiOperation> Operations(OpenApiDocument doc) =>
        doc.Paths.Values.SelectMany(p => p.Operations!.Values);
    private static OpenApiOperation Get(OpenApiDocument doc, string path, HttpMethod? method = null) =>
        doc.Paths[path].Operations![method ?? HttpMethod.Get];
    private static JsonNode Json(OpenApiDocument doc)
    {
        using var output = new StringWriter();
        doc.SerializeAsV3(new OpenApiJsonWriter(output));
        return JsonNode.Parse(output.ToString())!;
    }
    private static IOpenApiSchema Resolve(IOpenApiSchema schema, OpenApiDocument doc) =>
        schema is OpenApiSchemaReference reference ? doc.Components!.Schemas![reference.Reference.Id] : schema;
    private static void AssertTyped(IOpenApiSchema schema, OpenApiDocument doc, HashSet<string> visited)
    {
        if (schema is OpenApiSchemaReference reference && !visited.Add(reference.Reference.Id!)) return;
        schema = Resolve(schema, doc);
        Assert.True(schema.Type is not null || schema.AllOf?.Count > 0 || schema.OneOf?.Count > 0);
        if (schema.Type?.HasFlag(JsonSchemaType.Object) == true)
            Assert.True(schema.Properties?.Count > 0 || schema.AdditionalProperties is not null || schema.AllOf?.Count > 0, "Untyped object schema");
        foreach (var property in schema.Properties?.Values ?? []) AssertTyped(property, doc, visited);
        if (schema.Items is not null) AssertTyped(schema.Items, doc, visited);
        if (schema.AdditionalProperties is not null) AssertTyped(schema.AdditionalProperties, doc, visited);
        foreach (var part in schema.AllOf ?? []) AssertTyped(part, doc, visited);
    }
    private static void AssertExample(JsonNode? node, IOpenApiSchema schema, OpenApiDocument doc, string path)
    {
        schema = Resolve(schema, doc);
        if (node is null)
        {
            Assert.True(schema.Type?.HasFlag(JsonSchemaType.Null) == true, $"Unexpected null at {path}");
            return;
        }
        foreach (var part in schema.AllOf ?? []) AssertExample(node, part, doc, path);
        if (schema.AllOf?.Count > 0 && schema.Properties is not { Count: > 0 } && schema.AdditionalProperties is null) return;
        if (node is JsonObject obj)
        {
            Assert.True(schema.Type?.HasFlag(JsonSchemaType.Object) == true, $"Expected object at {path}");
            foreach (var name in schema.Required?.AsEnumerable() ?? Enumerable.Empty<string>()) Assert.True(obj.ContainsKey(name), $"Missing {path}/{name}");
            foreach (var (name, value) in obj)
            {
                if (schema.Properties?.TryGetValue(name, out var property) == true)
                    AssertExample(value, property, doc, path + "/" + name);
                else if (schema.AdditionalProperties is not null)
                    AssertExample(value, schema.AdditionalProperties, doc, path + "/" + name);
                else Assert.Fail($"Undocumented property {path}/{name}");
            }
        }
        else if (node is JsonArray array)
        {
            Assert.True(schema.Type?.HasFlag(JsonSchemaType.Array) == true, $"Expected array at {path}");
            foreach (var item in array) AssertExample(item, schema.Items!, doc, path + "[]");
        }
        else
        {
            var kind = node.GetValueKind();
            var expected = kind switch
            {
                JsonValueKind.String => JsonSchemaType.String,
                JsonValueKind.True or JsonValueKind.False => JsonSchemaType.Boolean,
                _ => schema.Type?.HasFlag(JsonSchemaType.Integer) == true ? JsonSchemaType.Integer : JsonSchemaType.Number
            };
            Assert.True(schema.Type?.HasFlag(expected) == true, $"Type mismatch at {path}: {kind} vs {schema.Type}");
        }
    }

    public void Dispose() => _services.Dispose();
}
