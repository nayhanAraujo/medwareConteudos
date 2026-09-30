using System.Text.Json;
using System.Text.Json.Nodes;

namespace MdwConteudos.Api.Modules.ApiPublica.Swagger;

public static class ApiPublicaOpenApiExamples
{
    // Entirely synthetic fixtures. Ranges illustrate the wire format, not clinical guidance.
    private const string Ts = "2026-01-15T12:00:00";
    public static JsonNode TokenRequest => Node(new { senha = "<SENHA_DO_PARCEIRO>" });
    public static JsonNode UpdateNormalidadeRequest => Node(new
    {
        valor_min = 20.0, valor_max = 40.0, sexo = "M", idade_min = 18, idade_max = (int?)null
    });
    public static JsonNode TokenOk => Node(new
    {
        success = true, token = "<JWT_DE_EXEMPLO_NAO_UTILIZAVEL>",
        expires_info = "Válido no dia atual (UTC) e por até 24h. Gere um novo token quando necessário."
    });
    public static JsonNode Token401 => Error("Não autorizado", "Senha inválida");
    public static JsonNode Token400 => Error("Senha não informada", "Envie um JSON com o campo \"senha\"");
    public static JsonNode Token503 => Error("Configuração do servidor", "Autenticação da API não configurada");
    public static JsonNode HealthOk => Node(new { success = true, status = "healthy", database = "connected", timestamp = Ts });
    public static JsonNode Health500 => Node(new
    {
        success = false, status = "unhealthy", database = "disconnected",
        error = "Falha de conexão com o banco de dados.", timestamp = Ts
    });
    private static JsonObject Variavel => (JsonObject)Node(new
    {
        codvariavel = 5, nome = "Diâmetro da Aorta", variavel = "DAORTA", sigla = "DAo",
        abreviacao = "DAo", descricao = (string?)null, casas_decimais = 1,
        unidade_medida = "mm", especialidades = new[] { "Cardiologia" }
    });
    private static JsonObject Referencia => (JsonObject)Node(new
    {
        codigo = 1, titulo = "Referência fictícia para documentação", ano = 2026,
        descricao = "Exemplo sintético, sem aplicação clínica.", autores = (string?)null
    });
    private static JsonObject Normalidade => (JsonObject)Node(new
    {
        codnormalidade = 120, sexo = "M", valor_min = 20.0, valor_max = 40.0,
        idade_min = 18, idade_max = (int?)null, referencia = Referencia
    });
    public static JsonNode VariaveisOk
    {
        get
        {
            var item = Variavel;
            item["nomes_clinicos"] = Node(new[] { "Diâmetro aórtico" });
            item["alternativas"] = Node(new[] { "Aorta" });
            return List(item);
        }
    }
    public static JsonNode VariavelDetalheOk
    {
        get
        {
            var normalidade = Normalidade;
            normalidade["classificacao"] = "Normal";
            normalidade["comentario_texto"] = "Faixa fictícia para demonstrar o contrato.";
            return Node(new
            {
                success = true,
                data = new
                {
                    variavel = Variavel,
                    normalidades = new[] { normalidade },
                    formulas = new[]
                    {
                        new
                        {
                            codformula = 2, codvariavel = 5, formula = "valor", casas_decimais = 1,
                            equacoes = new[] { new { codequacao = 3, equacao = "return valor;", linguagem = "JavaScript", referencia = Referencia } }
                        }
                    },
                    alternativas = new[] { "Aorta" },
                    comentarios = new[] { new { codigo = 1, sexo = "M", idade_min = 18, idade_max = -1, texto = "Faixa fictícia para demonstrar o contrato." } }
                },
                timestamp = Ts
            });
        }
    }
    public static JsonNode Variavel404 => Node(new
    {
        success = false, error = "Variável não encontrada", codvariavel_buscado = 99999,
        variaveis_exemplo = new[] { new { CODVARIAVEL = 5, NOME = "Diâmetro da Aorta" } }
    });
    public static JsonNode NormalidadesOk
    {
        get
        {
            var item = Normalidade;
            item["variavel"] = Node(new { codigo = 5, nome = "Diâmetro da Aorta", sigla = "DAo" });
            return List(item);
        }
    }
    private static JsonNode Faixa(double min, double max) => Node(new
    {
        min, max, _meta = new { Pagina = 1.0, Fonte = "Referência fictícia para documentação", Ano = 2026 }
    });
    public static JsonNode EcodopplerOk => Node(new
    {
        DAORTA = new
        {
            M = new
            {
                @default = Faixa(20, 40), low = Faixa(20, 25), moderated = Faixa(25, 30),
                elevated = Faixa(30, 35), high = Faixa(35, 40)
            }
        }
    });
    public static JsonNode Ecodoppler400 => Error("Parâmetro referencia inválido", "Informe um código de referência inteiro ≥ 1.");
    public static JsonNode Ecodoppler404 => Error("Referência não encontrada", "Não existe referência com CODREFERENCIA = 99.");
    public static JsonNode FormulasOk => Node(new
    {
        DAORTA = new[] { new { nome_funcao = "calcDAorta", equacao = "return valor;", linguagem = "JavaScript" } }
    });
    public static JsonNode ReferenciasOk
    {
        get
        {
            var item = Referencia;
            item["especialidade"] = "Cardiologia";
            return List(item);
        }
    }
    public static JsonNode EspecialidadesOk => List(Node(new
    {
        codigo = 1, nome = "Cardiologia", descricao = (string?)null, total_variaveis = 1
    }));
    public static JsonNode RelatoriosOk => List(Node(new
    {
        codrelatorio = 10, nome = "Relatório demonstrativo", modulo = "Laudos UX",
        formato = "FR3", dthrcriacao = Ts, ativo = 1, tem_multiselecao = false
    }));
    private static JsonObject Script => (JsonObject)Node(new
    {
        codscriptlaudo = 20, nome = "Modelo demonstrativo", descricao = "Exemplo sintético",
        linguagem = "JavaScript", sistema = "Laudos UX", aprovado = true, data_verificacao = Ts,
        ativo = true, aprovado_por = "", pacote_nome = "Cardiologia", codpacote = 1,
        numero_versao = "1.0",
        imagens = new[] { new { nome = "interface-exemplo.png", caminho = "uploads/exemplos/interface-exemplo.png" } },
        pdfs = new[] { new { nome = "modelo-exemplo.pdf", caminho = "uploads/exemplos/modelo-exemplo.pdf" } },
        mrds = new[] { new { codscriptmrd = (int?)null, codversaomrd = 30, nome_arquivo = "modelo-exemplo.mrd", padrao = true, ordem = 1 } },
        qtd_mrd = 1, mrd_fonte = "SCRIPT_VERSAO_MRD"
    });
    public static JsonNode ScriptsOk => List(Script);
    public static JsonNode ScriptUltimoVerificadoOk
    {
        get
        {
            var item = Script;
            item["imagem_capa"] = Node(new { indice = 0, nome = "interface-exemplo.png", caminho_relativo_api = "/apiconteudos/v1/scripts/20/imagem?indice=0" });
            return Node(new { success = true, data = item, workflow_key = "20|" + Ts, timestamp = Ts });
        }
    }
    public static JsonNode PaineisOk => List(Node(new
    {
        codpainel = 40, nome = "Painel demonstrativo", descricao = (string?)null, ativo = 1,
        tipo_painel = "POWERBI", codcliente = (int?)null, nome_cliente = (string?)null,
        codmodulo = 1, nome_modulo = "Atendimento", tem_arquivo_pbix = true,
        nome_arquivo_pbix = "painel-exemplo.pbix", pacotes = new[] { "Atendimento" }
    }));
    public static JsonNode SistemaInfoOk => Node(new
    {
        success = true,
        data = new
        {
            estatisticas = new
            {
                total_variaveis = 1, total_normalidades = 1, total_referencias = 1,
                total_autores = 0, total_especialidades = 1, variaveis_com_normalidade = 1, variaveis_com_formula = 1
            },
            variaveis_por_especialidade = new[] { new { especialidade = "Cardiologia", total = 1 } },
            versao_api = "1.0", ultima_atualizacao = Ts
        },
        timestamp = Ts
    });
    public static JsonNode ClienteNormalidadesOk
    {
        get
        {
            var data = EcodopplerOk;
            // Client rows do not select REFERENCIA_TITULO/ANO, so their range metadata only has Pagina.
            foreach (var faixa in data["DAORTA"]!["M"]!.AsObject().Select(p => p.Value!))
                faixa["_meta"] = Node(new { Pagina = 1.0 });
            data["DAORTA"]!["_comentario_texto"] = Node(new { texto = "Exemplo sem aplicação clínica.", A = "Exemplo sem aplicação clínica." });
            return Node(new
            {
                success = true, cliente = new { codigo = 100 },
                padrao = new { codigo = 2, codigo_api = "ECO_EXEMPLO", nome = "Padrão demonstrativo", vigente = true },
                data
            });
        }
    }
    public static JsonNode ClientePadroesOk => List(Node(new
    {
        codigo = 2, codigo_api = "ECO_EXEMPLO", nome = "Padrão demonstrativo",
        cod_referencia_origem = (int?)null, padrao_vigente = true, descricao = (string?)null
    }));
    public static JsonNode UpdateNormalidadeOk => Node(new { success = true, message = "Normalidade atualizada com sucesso", timestamp = Ts });
    public static JsonNode UpdateNormalidade400 => Error("valor_min e valor_max são obrigatórios");
    public static JsonNode UpdateNormalidade404 => Error("Normalidade não encontrada");
    public static JsonNode Resource404(string error) => Error(error);
    public static JsonNode Relatorio404 => Node(new { success = false, error = "Relatório não encontrado", codrelatorio = 10 });
    public static JsonNode Script404 => Node(new { success = false, error = "Script não encontrado", codscriptlaudo = 20 });
    public static JsonNode Script400 => Node(new { success = false, error = "DLL disponível apenas para scripts Laudos Flex", codscriptlaudo = 20 });
    public static JsonNode Painel404 => Node(new { success = false, error = "Painel não encontrado", codpainel = 40 });
    public static JsonNode Filtro400 => Error("Parâmetro inválido", "Use ativo=1 ou ativo=0");
    public static JsonNode Cliente404 => Error("Cliente não encontrado");
    public static JsonNode Error500 => Error("Erro interno do servidor", "Falha ao processar a solicitação.");
    public static JsonNode Unauthorized401 => Error("Não autorizado", "Header Authorization com Bearer JWT é obrigatório");
    private static JsonNode List(JsonNode item) => Node(new { success = true, data = new[] { item }, total = 1, timestamp = Ts });
    private static JsonNode Error(string error, string? message = null)
        => message is null ? Node(new { success = false, error }) : Node(new { success = false, error, message });
    private static JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value) ?? new JsonObject();
}
