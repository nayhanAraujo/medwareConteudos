using System.Text.Json;
using System.Text.Json.Nodes;

namespace MdwConteudos.Api.Modules.ApiPublica.Swagger;

public static class ApiPublicaOpenApiExamples
{
    private const string Ts = "2026-05-29T12:00:00.0000000-03:00";

    public static JsonNode TokenRequest => Node(new { senha = "sua_senha_parceiro" });
    public static JsonNode UpdateNormalidadeRequest => Node(new
    {
        valor_min = 10.5,
        valor_max = 50.0,
        sexo = "M",
        idade_min = 18,
        idade_max = 65
    });

    public static JsonNode TokenOk => Node(new
    {
        success = true,
        token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.exemplo",
        expires_info = "Válido no dia atual (UTC) e por até 24h. Gere um novo token quando necessário."
    });
    public static JsonNode Token401 => Error("Não autorizado", "Senha inválida");
    public static JsonNode Token400 => Error("Senha não informada", "Envie um JSON com o campo senha");
    public static JsonNode Token503 => Error("Configuração do servidor", "Autenticação da API não configurada");

    public static JsonNode HealthOk => Node(new
    {
        success = true,
        status = "healthy",
        database = "connected",
        timestamp = Ts
    });
    public static JsonNode Health500 => Node(new
    {
        success = false,
        status = "unhealthy",
        database = "disconnected",
        error = "Unable to complete network request to host localhost.",
        timestamp = Ts
    });

    public static JsonNode VariaveisOk => Node(new
    {
        success = true,
        data = new[]
        {
            new
            {
                codvariavel = 5,
                nome = "Diâmetro da Aorta",
                variavel = "DAORTA",
                sigla = "DAo",
                abreviacao = "DAo",
                descricao = (string?)null,
                casas_decimais = 1,
                unidade_medida = "mm",
                especialidades = new[] { "Cardiologia" }
            }
        },
        total = 1,
        timestamp = Ts
    });

    public static JsonNode VariavelDetalheOk => Node(new
    {
        success = true,
        data = new
        {
            variavel = new
            {
                codvariavel = 5,
                nome = "Diâmetro da Aorta",
                variavel = "DAORTA",
                sigla = "DAo",
                casas_decimais = 1,
                unidade_medida = "mm",
                especialidades = new[] { "Cardiologia" }
            },
            normalidades = new[]
            {
                new
                {
                    codnormalidade = 120,
                    sexo = "M",
                    valor_min = 20.0,
                    valor_max = 37.0,
                    referencia = new
                    {
                        codigo = 1,
                        titulo = "Recommendations for Cardiac Chamber Quantification",
                        ano = 2015
                    }
                }
            },
            formulas = Array.Empty<object>(),
            alternativas = new[] { "Aorta" }
        },
        timestamp = Ts
    });

    public static JsonNode Variavel404 => Node(new
    {
        success = false,
        error = "Variável não encontrada",
        codvariavel_buscado = 99999
    });

    public static JsonNode NormalidadesOk => Node(new
    {
        success = true,
        data = new[]
        {
            new
            {
                codnormalidade = 120,
                variavel = new { codigo = 5, nome = "Diâmetro da Aorta", sigla = "DAo" },
                sexo = "M",
                valor_min = 20.0,
                valor_max = 37.0
            }
        },
        total = 1,
        timestamp = Ts
    });

    public static JsonNode EcodopplerOk => Node(new
    {
        DAORTA = new
        {
            M = new
            {
                low = new { min = 20.0, max = 24.25, _meta = new { Fonte = "ASE 2015" } },
                high = new { min = 32.75, max = 37.0 }
            }
        }
    });
    public static JsonNode Ecodoppler400 => Error("Parâmetro referencia inválido", "Informe um código de referência inteiro maior ou igual a 1.");
    public static JsonNode Ecodoppler404 => Error("Referência não encontrada", "Não existe referência com CODREFERENCIA = 99.");

    public static JsonNode FormulasOk => Node(new
    {
        DAORTA = new[] { new { nome_funcao = "calcDAorta", equacao = "return valor * 1.0;", linguagem = "JavaScript" } }
    });

    public static JsonNode SistemaInfoOk => Node(new
    {
        success = true,
        data = new { variaveis = 450, normalidades = 3200, referencias = 85, scripts = 120 },
        timestamp = Ts
    });

    public static JsonNode UpdateNormalidadeOk => Node(new
    {
        success = true,
        message = "Normalidade atualizada com sucesso.",
        timestamp = Ts
    });
    public static JsonNode UpdateNormalidade400 => Error("valor_min e valor_max são obrigatórios");
    public static JsonNode UpdateNormalidade404 => Error("Normalidade não encontrada");
    public static JsonNode Resource404(string error) => Error(error);
    public static JsonNode Error500 => Error("Erro interno do servidor", "Detalhe técnico da exceção");
    public static JsonNode Unauthorized401 => Error("Não autorizado", "Token JWT inválido ou expirado");
    public static JsonNode Empty => new JsonObject();

    private static JsonNode Error(string error, string? message = null)
        => Node(new { success = false, error, message });

    private static JsonNode Node<T>(T value)
        => JsonSerializer.SerializeToNode(value) ?? new JsonObject();
}
