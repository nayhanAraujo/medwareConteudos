using Microsoft.OpenApi.Any;

namespace MdwConteudos.Api.Modules.ApiPublica.Swagger;

public static class ApiPublicaOpenApiExamples
{
    private const string Ts = "2026-05-29T12:00:00.0000000-03:00";

    public static OpenApiObject TokenRequest => new()
    {
        ["senha"] = new OpenApiString("sua_senha_parceiro")
    };

    public static OpenApiObject UpdateNormalidadeRequest => new()
    {
        ["valor_min"] = new OpenApiDouble(10.5),
        ["valor_max"] = new OpenApiDouble(50.0),
        ["sexo"] = new OpenApiString("M"),
        ["idade_min"] = new OpenApiInteger(18),
        ["idade_max"] = new OpenApiInteger(65)
    };

    public static OpenApiObject TokenOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["token"] = new OpenApiString("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzZW5oYSI6Ii4uLiIsImRhdGFob3JhIjoiMjAyNi0wNS0yOVQxNTowMDowMFoifQ.exemplo"),
        ["expires_info"] = new OpenApiString("Válido no dia atual (UTC) e por até 24h. Gere um novo token quando necessário.")
    };

    public static OpenApiObject Token401 => Error("Não autorizado", "Senha inválida");
    public static OpenApiObject Token400 => Error("Senha não informada", "Envie um JSON com o campo \"senha\"");
    public static OpenApiObject Token503 => Error("Configuração do servidor", "Autenticação da API não configurada");

    public static OpenApiObject HealthOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["status"] = new OpenApiString("healthy"),
        ["database"] = new OpenApiString("connected"),
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject Health500 => new()
    {
        ["success"] = new OpenApiBoolean(false),
        ["status"] = new OpenApiString("unhealthy"),
        ["database"] = new OpenApiString("disconnected"),
        ["error"] = new OpenApiString("Unable to complete network request to host \"localhost\"."),
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject VariaveisOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["data"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["codvariavel"] = new OpenApiInteger(5),
                ["nome"] = new OpenApiString("Diâmetro da Aorta"),
                ["variavel"] = new OpenApiString("DAORTA"),
                ["sigla"] = new OpenApiString("DAo"),
                ["abreviacao"] = new OpenApiString("DAo"),
                ["descricao"] = new OpenApiNull(),
                ["casas_decimais"] = new OpenApiInteger(1),
                ["unidade_medida"] = new OpenApiString("mm"),
                ["especialidades"] = new OpenApiArray { new OpenApiString("Cardiologia") }
            }
        },
        ["total"] = new OpenApiInteger(1),
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject VariavelDetalheOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["data"] = new OpenApiObject
        {
            ["variavel"] = new OpenApiObject
            {
                ["codvariavel"] = new OpenApiInteger(5),
                ["nome"] = new OpenApiString("Diâmetro da Aorta"),
                ["variavel"] = new OpenApiString("DAORTA"),
                ["sigla"] = new OpenApiString("DAo"),
                ["casas_decimais"] = new OpenApiInteger(1),
                ["unidade_medida"] = new OpenApiString("mm"),
                ["especialidades"] = new OpenApiArray { new OpenApiString("Cardiologia") }
            },
            ["normalidades"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["codnormalidade"] = new OpenApiInteger(120),
                    ["sexo"] = new OpenApiString("M"),
                    ["valor_min"] = new OpenApiDouble(20.0),
                    ["valor_max"] = new OpenApiDouble(37.0),
                    ["referencia"] = new OpenApiObject
                    {
                        ["codigo"] = new OpenApiInteger(1),
                        ["titulo"] = new OpenApiString("Recommendations for Cardiac Chamber Quantification"),
                        ["ano"] = new OpenApiInteger(2015)
                    }
                }
            },
            ["formulas"] = new OpenApiArray(),
            ["alternativas"] = new OpenApiArray { new OpenApiString("Aorta") }
        },
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject Variavel404 => new()
    {
        ["success"] = new OpenApiBoolean(false),
        ["error"] = new OpenApiString("Variável não encontrada"),
        ["codvariavel_buscado"] = new OpenApiInteger(99999)
    };

    public static OpenApiObject NormalidadesOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["data"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["codnormalidade"] = new OpenApiInteger(120),
                ["variavel"] = new OpenApiObject
                {
                    ["codigo"] = new OpenApiInteger(5),
                    ["nome"] = new OpenApiString("Diâmetro da Aorta"),
                    ["sigla"] = new OpenApiString("DAo")
                },
                ["sexo"] = new OpenApiString("M"),
                ["valor_min"] = new OpenApiDouble(20.0),
                ["valor_max"] = new OpenApiDouble(37.0)
            }
        },
        ["total"] = new OpenApiInteger(1),
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject EcodopplerOk => new()
    {
        ["DAORTA"] = new OpenApiObject
        {
            ["M"] = new OpenApiObject
            {
                ["low"] = new OpenApiObject
                {
                    ["min"] = new OpenApiDouble(20.0),
                    ["max"] = new OpenApiDouble(24.25),
                    ["_meta"] = new OpenApiObject { ["Fonte"] = new OpenApiString("ASE 2015") }
                },
                ["high"] = new OpenApiObject
                {
                    ["min"] = new OpenApiDouble(32.75),
                    ["max"] = new OpenApiDouble(37.0)
                }
            }
        }
    };

    public static OpenApiObject Ecodoppler400 => Error("Parâmetro referencia inválido", "Informe um código de referência inteiro ≥ 1.");
    public static OpenApiObject Ecodoppler404 => Error("Referência não encontrada", "Não existe referência com CODREFERENCIA = 99.");

    public static OpenApiObject FormulasOk => new()
    {
        ["DAORTA"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["nome_funcao"] = new OpenApiString("calcDAorta"),
                ["equacao"] = new OpenApiString("return valor * 1.0;"),
                ["linguagem"] = new OpenApiString("JavaScript")
            }
        }
    };

    public static OpenApiObject SistemaInfoOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["data"] = new OpenApiObject
        {
            ["variaveis"] = new OpenApiInteger(450),
            ["normalidades"] = new OpenApiInteger(3200),
            ["referencias"] = new OpenApiInteger(85),
            ["scripts"] = new OpenApiInteger(120)
        },
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject UpdateNormalidadeOk => new()
    {
        ["success"] = new OpenApiBoolean(true),
        ["message"] = new OpenApiString("Normalidade atualizada com sucesso."),
        ["timestamp"] = new OpenApiString(Ts)
    };

    public static OpenApiObject UpdateNormalidade400 => Error("valor_min e valor_max são obrigatórios");
    public static OpenApiObject UpdateNormalidade404 => Error("Normalidade não encontrada");

    public static OpenApiObject Resource404(string error) => new()
    {
        ["success"] = new OpenApiBoolean(false),
        ["error"] = new OpenApiString(error)
    };

    public static OpenApiObject Error500 => Error("Erro interno do servidor", "Detalhe técnico da exceção");
    public static OpenApiObject Unauthorized401 => Error("Não autorizado", "Token JWT inválido ou expirado");

    private static OpenApiObject Error(string error, string? message = null)
    {
        var o = new OpenApiObject
        {
            ["success"] = new OpenApiBoolean(false),
            ["error"] = new OpenApiString(error)
        };
        if (message != null)
            o["message"] = new OpenApiString(message);
        return o;
    }
}
