using System.Text.Json.Nodes;
using System.Text.Json;
using MdwConteudos.Api.Modules.Avaliacoes;
using Microsoft.OpenApi;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public class ApiPublicaExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType == typeof(AvaliacaoPublicaController))
        {
            EvaluationExamples(operation, context.MethodInfo.Name);
            return;
        }
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

    private static void EvaluationExamples(OpenApiOperation operation, string action)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var date = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var review = new AvaliacaoPublica(1, 20, 30, "1.0", "João", 4, "sim", 5, 4, 4, 5, 4,
            "Bom modelo", "Conteúdo útil e fácil de usar.", date, date);
        if (operation.RequestBody?.Content?.TryGetValue("application/json", out var request) == true)
            request.Example = JsonSerializer.SerializeToNode(new AvaliacaoInput {
                CodVersao = 30, Origem = "portal-modelos", ClienteId = "clinica-123", UsuarioId = "usuario-456",
                NomeAvaliador = "João", NomeClinica = "Clínica Exemplo", NotaGeral = 4, Recomendacao = "sim",
                FacilidadeUso = 5, QualidadeConteudo = 4, Completude = 4, Aparencia = 5, UtilidadeClinica = 4,
                Titulo = review.Titulo, Comentario = review.Comentario
            }, options);
        if (operation.Responses is null) return;
        foreach (var (code, response) in operation.Responses)
        {
            JsonNode? example;
            if (code is "200" or "201")
            {
                object body = action switch {
                    nameof(AvaliacaoPublicaController.Save) => new AvaliacaoEnvelope<AvaliacaoSave>(true, new(1, code == "201", code != "201", review)),
                    nameof(AvaliacaoPublicaController.List) => new AvaliacaoPublicPage(true, [review], 1, 25, 1, 1),
                    nameof(AvaliacaoPublicaController.Summary) => new AvaliacaoEnvelope<AvaliacaoResumo>(true, new() { Quantidade = 1, MediaGeral = 4, FacilidadeUso = 5, QualidadeConteudo = 4, Completude = 4, Aparencia = 5, UtilidadeClinica = 4, DistribuicaoNotas = new() { [1] = 0, [2] = 0, [3] = 0, [4] = 1, [5] = 0 }, Recomendacoes = new() { ["sim"] = 1, ["talvez"] = 0, ["nao"] = 0 } }),
                    _ => new AvaliacaoEnvelope<AvaliacaoPublica>(true, review)
                };
                example = JsonSerializer.SerializeToNode(body, options);
            }
            else example = Example("", code);
            if (example is not null && response.Content is not null)
                foreach (var (mime, media) in response.Content)
                    if (mime == "application/json") media.Example = example.DeepClone();
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
            "Pacotes" => ApiPublicaOpenApiExamples.PacotesOk,
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
            "ModuloRelatorioIcone" => ApiPublicaOpenApiExamples.ModuloRelatorio404,
            "PacoteIcone" => ApiPublicaOpenApiExamples.Pacote404,
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
