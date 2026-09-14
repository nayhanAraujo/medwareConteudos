using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public class ApiPublicaExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ApiPublicaController)) return;
        var action = context.MethodInfo.Name;
        if (operation.RequestBody?.Content?.TryGetValue("application/json", out var request) == true)
            request.Example = action switch
            {
                "Token" => ApiPublicaOpenApiExamples.TokenRequest,
                "UpdateNormalidade" => ApiPublicaOpenApiExamples.UpdateNormalidadeRequest,
                _ => null
            };

        if (operation.Responses is null) return;
        foreach (var (code, response) in operation.Responses)
        {
            if (code == "204")
            {
                response.Content?.Clear();
                continue;
            }
            // Never manufacture JSON for bodyless responses or binary success content.
            var example = Example(action, code);
            if (example is null || response.Content is null) continue;
            foreach (var (mediaType, media) in response.Content)
                if (mediaType == "application/json") media.Example = example.DeepClone();
        }
    }

    private static JsonNode? Example(string action, string code) => code switch
    {
        "200" => action switch
        {
            "Token" => ApiPublicaOpenApiExamples.TokenOk,
            "Health" => ApiPublicaOpenApiExamples.HealthOk,
            "Variaveis" => ApiPublicaOpenApiExamples.VariaveisOk,
            "VariavelDetalhe" => ApiPublicaOpenApiExamples.VariavelDetalheOk,
            "Normalidades" => ApiPublicaOpenApiExamples.NormalidadesOk,
            "Ecodoppler" or "EcodopplerPorRef" => ApiPublicaOpenApiExamples.EcodopplerOk,
            "Formulas" => ApiPublicaOpenApiExamples.FormulasOk,
            "Referencias" => ApiPublicaOpenApiExamples.ReferenciasOk,
            "Especialidades" => ApiPublicaOpenApiExamples.EspecialidadesOk,
            "Relatorios" => ApiPublicaOpenApiExamples.RelatoriosOk,
            "Scripts" => ApiPublicaOpenApiExamples.ScriptsOk,
            "ScriptUltimoVerificado" => ApiPublicaOpenApiExamples.ScriptUltimoVerificadoOk,
            "Paineis" => ApiPublicaOpenApiExamples.PaineisOk,
            "SistemaInfo" => ApiPublicaOpenApiExamples.SistemaInfoOk,
            "UpdateNormalidade" => ApiPublicaOpenApiExamples.UpdateNormalidadeOk,
            "ClienteNormalidades" => ApiPublicaOpenApiExamples.ClienteNormalidadesOk,
            "ClientePadroesNormalidade" => ApiPublicaOpenApiExamples.ClientePadroesOk,
            _ => null
        },
        "400" => action switch
        {
            "Token" => ApiPublicaOpenApiExamples.Token400,
            "UpdateNormalidade" => ApiPublicaOpenApiExamples.UpdateNormalidade400,
            "Ecodoppler" or "EcodopplerPorRef" => ApiPublicaOpenApiExamples.Ecodoppler400,
            "DownloadScript" => ApiPublicaOpenApiExamples.Script400,
            "Relatorios" or "Paineis" => ApiPublicaOpenApiExamples.Filtro400,
            _ => null
        },
        "401" => action == "Token" ? ApiPublicaOpenApiExamples.Token401 : ApiPublicaOpenApiExamples.Unauthorized401,
        "404" => action switch
        {
            "VariavelDetalhe" => ApiPublicaOpenApiExamples.Variavel404,
            "Ecodoppler" or "EcodopplerPorRef" => ApiPublicaOpenApiExamples.Ecodoppler404,
            "UpdateNormalidade" => ApiPublicaOpenApiExamples.UpdateNormalidade404,
            "DownloadRelatorio" => ApiPublicaOpenApiExamples.Relatorio404,
            "DownloadScript" or "ScriptImagem" => ApiPublicaOpenApiExamples.Script404,
            "DownloadPainel" => ApiPublicaOpenApiExamples.Painel404,
            "ClienteNormalidades" or "ClientePadroesNormalidade" => ApiPublicaOpenApiExamples.Cliente404,
            _ => null
        },
        "500" => action == "Health" ? ApiPublicaOpenApiExamples.Health500 : ApiPublicaOpenApiExamples.Error500,
        "503" => ApiPublicaOpenApiExamples.Token503,
        _ => null
    };
}
