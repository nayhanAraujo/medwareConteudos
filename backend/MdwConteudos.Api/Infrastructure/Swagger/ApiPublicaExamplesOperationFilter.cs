using Microsoft.OpenApi.Models;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public class ApiPublicaExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType?.Name != "ApiPublicaController")
            return;

        SetRequestExample(operation, context.MethodInfo.Name);
        SetResponseExamples(operation, context.MethodInfo.Name);
    }

    private static void SetRequestExample(OpenApiOperation operation, string action)
    {
        if (operation.RequestBody?.Content == null)
            return;

        var example = action switch
        {
            "Token" => ApiPublicaOpenApiExamples.TokenRequest,
            "UpdateNormalidade" => ApiPublicaOpenApiExamples.UpdateNormalidadeRequest,
            _ => null
        };
        if (example == null)
            return;

        foreach (var media in operation.RequestBody.Content.Values)
            media.Example = example;
    }

    private static void SetResponseExamples(OpenApiOperation operation, string action)
    {
        var map = GetResponseMap(action);
        foreach (var (code, example) in map)
        {
            if (!operation.Responses.TryGetValue(code, out var response))
                continue;
            EnsureJsonContent(response);
            response.Content!["application/json"].Example = example;
        }

        if (action is not ("Token" or "Health") &&
            operation.Responses.TryGetValue("401", out var unauthorized))
        {
            EnsureJsonContent(unauthorized);
            unauthorized.Content!["application/json"].Example = ApiPublicaOpenApiExamples.Unauthorized401;
        }
    }

    private static Dictionary<string, Microsoft.OpenApi.Any.IOpenApiAny> GetResponseMap(string action) =>
        action switch
        {
            "Token" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.TokenOk,
                ["400"] = ApiPublicaOpenApiExamples.Token400,
                ["401"] = ApiPublicaOpenApiExamples.Token401,
                ["503"] = ApiPublicaOpenApiExamples.Token503
            },
            "Health" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.HealthOk,
                ["500"] = ApiPublicaOpenApiExamples.Health500
            },
            "Variaveis" or "Normalidades" or "Referencias" or "Especialidades" or "Relatorios"
                or "Scripts" or "Paineis" => new()
            {
                ["200"] = action switch
                {
                    "Variaveis" => ApiPublicaOpenApiExamples.VariaveisOk,
                    "Normalidades" => ApiPublicaOpenApiExamples.NormalidadesOk,
                    _ => ApiPublicaOpenApiExamples.VariaveisOk
                },
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "VariavelDetalhe" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.VariavelDetalheOk,
                ["404"] = ApiPublicaOpenApiExamples.Variavel404,
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "Ecodoppler" or "EcodopplerPorRef" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.EcodopplerOk,
                ["400"] = ApiPublicaOpenApiExamples.Ecodoppler400,
                ["404"] = ApiPublicaOpenApiExamples.Ecodoppler404,
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "Formulas" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.FormulasOk,
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "SistemaInfo" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.SistemaInfoOk,
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "UpdateNormalidade" => new()
            {
                ["200"] = ApiPublicaOpenApiExamples.UpdateNormalidadeOk,
                ["400"] = ApiPublicaOpenApiExamples.UpdateNormalidade400,
                ["404"] = ApiPublicaOpenApiExamples.UpdateNormalidade404,
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "DownloadRelatorio" => new()
            {
                ["404"] = ApiPublicaOpenApiExamples.Resource404("Relatório não encontrado"),
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "ScriptImagem" or "DownloadScript" => new()
            {
                ["404"] = ApiPublicaOpenApiExamples.Resource404("Script não encontrado"),
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "ScriptUltimoVerificado" => new()
            {
                ["204"] = new Microsoft.OpenApi.Any.OpenApiObject(),
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            "DownloadPainel" => new()
            {
                ["404"] = ApiPublicaOpenApiExamples.Resource404("Painel não encontrado"),
                ["500"] = ApiPublicaOpenApiExamples.Error500
            },
            _ => new()
            {
                ["500"] = ApiPublicaOpenApiExamples.Error500
            }
        };

    private static void EnsureJsonContent(OpenApiResponse response)
    {
        response.Content ??= new Dictionary<string, OpenApiMediaType>();
        if (!response.Content.ContainsKey("application/json"))
            response.Content["application/json"] = new OpenApiMediaType();
    }
}
