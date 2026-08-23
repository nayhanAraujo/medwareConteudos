using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.ApiPublica.Swagger;
using Swashbuckle.AspNetCore.Annotations;

namespace MdwConteudos.Api.Modules.ApiPublica;

[ApiController]
[SwaggerTag("API pública MDW Conteúdos (variáveis, scripts, relatórios, painéis)")]
public class ApiPublicaController : ControllerBase
{
    private readonly IApiPublicaService _api;

    public ApiPublicaController(IApiPublicaService api) => _api = api;

    [HttpPost("apiconteudos/v1/token")]
    [HttpPost("api/v1/token")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Obter token JWT",
        Description = "Envie a senha de parceiro (`API_JWT_PASSWORD`) em JSON. Retorna JWT válido no dia atual (UTC) por até 24h. **Não exige Authorize.**",
        Tags = new[] { "Sistema" })]
    [SwaggerResponse(200, "Token gerado com sucesso", typeof(TokenSuccessDoc))]
    [SwaggerResponse(400, "Senha não informada ou JSON inválido", typeof(ApiErrorDoc))]
    [SwaggerResponse(401, "Senha inválida", typeof(ApiErrorDoc))]
    [SwaggerResponse(503, "Autenticação da API não configurada no servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Token([FromBody] TokenRequest? body, CancellationToken ct) =>
        _api.ObterTokenAsync(body, ct);

    [HttpGet("apiconteudos/v1/health")]
    [HttpGet("api/v1/health")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Health check",
        Description = "Verifica se a API e o banco Firebird estão respondendo. **Não exige JWT.**",
        Tags = new[] { "Sistema" })]
    [SwaggerResponse(200, "API e banco operacionais", typeof(HealthSuccessDoc))]
    [SwaggerResponse(500, "Banco indisponível ou erro interno", typeof(HealthErrorDoc))]
    public Task<IActionResult> Health(CancellationToken ct) => _api.HealthAsync(ct);

    [HttpGet("apiconteudos/v1/variaveis")]
    [HttpGet("api/v1/variaveis")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar variáveis",
        Description = "Retorna variáveis cadastradas com especialidades e unidade de medida.",
        Tags = new[] { "Variáveis" })]
    [SwaggerResponse(200, "Lista de variáveis", typeof(ApiListDoc<VariavelItemDoc>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Variaveis(
        [FromQuery, SwaggerParameter(Description = "Filtrar por nome da especialidade")] string? especialidade,
        [FromQuery, SwaggerParameter(Description = "Filtrar por descrição da unidade de medida")] string? unidade,
        CancellationToken ct) =>
        _api.GetVariaveisAsync(especialidade, unidade, ct);

    [HttpGet("apiconteudos/v1/variaveis/{codvariavel:int}")]
    [HttpGet("api/v1/variaveis/{codvariavel:int}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Detalhar variável",
        Description = "Retorna dados da variável, normalidades, fórmulas e alternativas vinculadas.",
        Tags = new[] { "Variáveis" })]
    [SwaggerResponse(200, "Detalhes da variável", typeof(VariavelDetalheDoc))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Variável não encontrada", typeof(VariavelNotFoundDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> VariavelDetalhe(
        [SwaggerParameter(Description = "Código da variável (CODVARIAVEL)", Required = true)] int codvariavel,
        CancellationToken ct) =>
        _api.GetVariavelDetalhadaAsync(codvariavel, ct);

    [HttpGet("apiconteudos/v1/normalidades")]
    [HttpGet("api/v1/normalidades")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar normalidades",
        Description = "Lista normalidades com filtros opcionais por variável, sexo e referência.",
        Tags = new[] { "Normalidades" })]
    [SwaggerResponse(200, "Lista de normalidades", typeof(ApiListDoc<NormalidadeItemDoc>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Normalidades(
        [FromQuery, SwaggerParameter(Description = "CODVARIAVEL (número) ou nome da variável (texto)")] string? variavel,
        [FromQuery, SwaggerParameter(Description = "M, F ou U")] string? sexo,
        [FromQuery, SwaggerParameter(Description = "CODREFERENCIA")] int? referencia,
        CancellationToken ct) =>
        _api.GetNormalidadesAsync(variavel, sexo, referencia, ct);

    [HttpGet("apiconteudos/v1/normalidades_ecodopplercardiograma")]
    [HttpGet("api/v1/normalidades_ecodopplercardiograma")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Normalidades ecodoppler (query)",
        Description = "Retorna mapa de normalidades ecocardiográficas por variável/sexo/zona para a referência informada.",
        Tags = new[] { "Normalidades" })]
    [SwaggerResponse(200, "Mapa de normalidades ecocardiográficas", typeof(Dictionary<string, Dictionary<string, Dictionary<string, object>>>))]
    [SwaggerResponse(400, "Parâmetro referencia inválido", typeof(ApiErrorDoc))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Referência não encontrada", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Ecodoppler(
        [FromQuery, SwaggerParameter(Description = "CODREFERENCIA (padrão: 1)")] int? referencia,
        CancellationToken ct) =>
        _api.GetEcodopplerAsync(referencia ?? 1, ct);

    [HttpGet("apiconteudos/v1/normalidades/ecodopplercardiograma/{referencia:int}")]
    [HttpGet("api/v1/normalidades/ecodopplercardiograma/{referencia:int}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Normalidades ecodoppler (path)",
        Description = "Mesmo retorno de `/normalidades_ecodopplercardiograma`, com referência na URL.",
        Tags = new[] { "Normalidades" })]
    [SwaggerResponse(200, "Mapa de normalidades ecocardiográficas", typeof(Dictionary<string, Dictionary<string, Dictionary<string, object>>>))]
    [SwaggerResponse(400, "Parâmetro referencia inválido", typeof(ApiErrorDoc))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Referência não encontrada", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> EcodopplerPorRef(
        [SwaggerParameter(Description = "CODREFERENCIA ≥ 1", Required = true)] int referencia,
        CancellationToken ct) =>
        _api.GetEcodopplerAsync(referencia, ct);

    [HttpOptions("apiconteudos/v1/normalidades_ecodopplercardiograma")]
    [HttpOptions("api/v1/normalidades_ecodopplercardiograma")]
    [HttpOptions("apiconteudos/v1/formulas")]
    [HttpOptions("api/v1/formulas")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult OptionsPreflight() => Ok();

    [HttpGet("apiconteudos/v1/formulas")]
    [HttpGet("api/v1/formulas")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar fórmulas",
        Description = "Retorna fórmulas agrupadas por variável, com equações por linguagem.",
        Tags = new[] { "Variáveis" })]
    [SwaggerResponse(200, "Fórmulas agrupadas por variável", typeof(Dictionary<string, List<object>>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Formulas(CancellationToken ct) => _api.GetFormulasAsync(ct);

    [HttpGet("apiconteudos/v1/referencias")]
    [HttpGet("api/v1/referencias")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar referências bibliográficas",
        Tags = new[] { "Referências" })]
    [SwaggerResponse(200, "Lista de referências", typeof(ApiListDoc<ReferenciaResumoDoc>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Referencias(
        [FromQuery, SwaggerParameter(Description = "Ano da referência")] int? ano,
        [FromQuery, SwaggerParameter(Description = "Nome da especialidade")] string? especialidade,
        [FromQuery, SwaggerParameter(Description = "Nome do autor (parcial)")] string? autor,
        CancellationToken ct) =>
        _api.GetReferenciasAsync(ano, especialidade, autor, ct);

    [HttpGet("apiconteudos/v1/sistema/info")]
    [HttpGet("api/v1/sistema/info")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Informações do sistema",
        Description = "Estatísticas do banco (totais de variáveis, normalidades, referências, etc.).",
        Tags = new[] { "Sistema" })]
    [SwaggerResponse(200, "Estatísticas do sistema", typeof(SistemaInfoDoc))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> SistemaInfo(CancellationToken ct) => _api.GetSistemaInfoAsync(ct);

    [HttpGet("apiconteudos/v1/especialidades")]
    [HttpGet("api/v1/especialidades")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar especialidades",
        Description = "Especialidades médicas com contagem de variáveis vinculadas.",
        Tags = new[] { "Referências" })]
    [SwaggerResponse(200, "Lista de especialidades", typeof(ApiListDoc<object>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Especialidades(CancellationToken ct) => _api.GetEspecialidadesAsync(ct);

    [HttpGet("apiconteudos/v1/relatorios")]
    [HttpGet("api/v1/relatorios")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar relatórios",
        Description = "Lista relatórios cadastrados (metadados; use download para o arquivo).",
        Tags = new[] { "Relatórios" })]
    [SwaggerResponse(200, "Lista de relatórios", typeof(ApiListDoc<object>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Relatorios(
        [FromQuery, SwaggerParameter(Description = "1 = ativo, 0 = inativo")] string? ativo,
        [FromQuery, SwaggerParameter(Description = "1 = com multiseleção, 0 = sem")] string? multiselecao,
        [FromQuery, SwaggerParameter(Description = "Filtro parcial em nome ou módulo")] string? nome,
        [FromQuery, SwaggerParameter(Description = "Módulo exato (Laudos UX, Financeiro, etc.)")] string? modulo,
        [FromQuery, SwaggerParameter(Description = "Formato exato (XML, JSON, etc.)")] string? formato,
        [FromQuery, SwaggerParameter(Description = "CODSISTEMA via SISTEMA_MODULO")] int? codsistema,
        CancellationToken ct) =>
        _api.GetRelatoriosAsync(ativo, multiselecao, nome, modulo, formato, codsistema, ct);

    [HttpGet("apiconteudos/v1/relatorios/{codrelatorio:int}/download")]
    [HttpGet("api/v1/relatorios/{codrelatorio:int}/download")]
    [SwaggerOperation(
        Summary = "Download de relatório",
        Description = "Baixa o arquivo binário do relatório (attachment). Resposta 200 é `application/octet-stream`.",
        Tags = new[] { "Relatórios" })]
    [SwaggerResponse(200, "Arquivo do relatório (binário)")]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Relatório não encontrado", typeof(ResourceNotFoundDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> DownloadRelatorio(
        [SwaggerParameter(Description = "CODRELATORIO", Required = true)] int codrelatorio,
        CancellationToken ct) =>
        _api.DownloadRelatorioAsync(codrelatorio, ct);

    [HttpGet("apiconteudos/v1/scripts")]
    [HttpGet("api/v1/scripts")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar scripts",
        Description =
            "Lista modelos de laudo. Com `incluir_arquivos=1` (padrão), inclui imagens, PDFs e MRDs " +
            "(prioriza versão ATIVA quando existir). Use `incluir_arquivos=0` para resposta enxuta.",
        Tags = new[] { "Scripts" })]
    [SwaggerResponse(200, "Lista de scripts", typeof(ApiListDoc<object>))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Scripts(
        [FromQuery, SwaggerParameter(Description = "Laudos UX ou Laudos Flex")] string? sistema,
        [FromQuery, SwaggerParameter(Description = "1 = aprovado, 0 = não aprovado")] string? aprovado,
        [FromQuery, SwaggerParameter(Description = "1 = ativo, 0 = inativo")] string? ativo,
        [FromQuery, SwaggerParameter(Description = "CODPACOTE (ex.: 1 Cardiologia, 14 Oftalmologia)")] string? pacote,
        [FromQuery, SwaggerParameter(Description = "1 inclui arquivos (padrão); 0 omite imagens/pdfs/mrds")] string? incluir_arquivos,
        CancellationToken ct) =>
        _api.GetScriptsAsync(sistema, aprovado, ativo, pacote, incluir_arquivos, ct);

    [HttpGet("apiconteudos/v1/scripts/ultimo-verificado")]
    [HttpGet("api/v1/scripts/ultimo-verificado")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Último script verificado",
        Description =
            "Retorna o script mais recente com `data_verificacao` preenchida (mesmos filtros de GET /scripts). " +
            "Resposta 204 se nenhum atender. Inclui `workflow_key` para deduplicação (n8n).",
        Tags = new[] { "Scripts" })]
    [SwaggerResponse(200, "Script encontrado", typeof(object))]
    [SwaggerResponse(204, "Nenhum script verificado atende aos filtros")]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> ScriptUltimoVerificado(
        [FromQuery] string? sistema,
        [FromQuery] string? aprovado,
        [FromQuery] string? ativo,
        [FromQuery] string? pacote,
        [FromQuery] string? incluir_arquivos,
        CancellationToken ct) =>
        _api.GetScriptUltimoVerificadoAsync(sistema, aprovado, ativo, pacote, incluir_arquivos, ct);

    [HttpGet("apiconteudos/v1/scripts/{codscriptlaudo:int}/imagem")]
    [HttpGet("api/v1/scripts/{codscriptlaudo:int}/imagem")]
    [SwaggerOperation(
        Summary = "Imagem do script",
        Description = "Retorna arquivo de interface (PNG/JPG). `indice=0` é a primeira imagem da listagem.",
        Tags = new[] { "Scripts" })]
    [SwaggerResponse(200, "Imagem (image/png ou image/jpeg)")]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Script ou imagem não encontrada", typeof(ResourceNotFoundDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> ScriptImagem(
        [SwaggerParameter(Description = "CODSCRIPTLAUDO", Required = true)] int codscriptlaudo,
        [FromQuery, SwaggerParameter(Description = "Posição no array imagens (0 = primeira)")] int indice = 0,
        CancellationToken ct = default) =>
        _api.GetScriptImagemAsync(codscriptlaudo, indice, ct);

    [HttpGet("apiconteudos/v1/scripts/{codscriptlaudo:int}/download")]
    [HttpGet("api/v1/scripts/{codscriptlaudo:int}/download")]
    [SwaggerOperation(
        Summary = "Download de arquivos do script",
        Description =
            "Baixa JSON/DLL/MRD conforme sistema. Preferência à versão ATIVA em SCRIPTVERSOES. " +
            "Parâmetro `tipo`: json, dll, mrd, mrd_todos (arquivo único em ZIP). " +
            "Sem `tipo`: pacote padrão (UX=JSON+MRD, Flex=DLL+MRD) em ZIP. " +
            "Headers: X-Script-Download-Suffix, X-Script-Download-Source.",
        Tags = new[] { "Scripts" })]
    [SwaggerResponse(200, "Arquivo ou ZIP (application/octet-stream ou application/zip)")]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Script ou arquivo não encontrado", typeof(ResourceNotFoundDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> DownloadScript(
        [SwaggerParameter(Description = "CODSCRIPTLAUDO", Required = true)] int codscriptlaudo,
        [FromQuery, SwaggerParameter(Description = "Opcional: json, dll, mrd ou mrd_todos")] string? tipo,
        CancellationToken ct) =>
        _api.DownloadScriptAsync(codscriptlaudo, tipo, ct);

    [HttpGet("apiconteudos/v1/paineis")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Listar painéis",
        Description = "Lista painéis Power BI/API (sem binário PBIX; use download). **Somente /apiconteudos/v1.**",
        Tags = new[] { "Painéis" })]
    [SwaggerResponse(200, "Lista de painéis", typeof(ApiListDoc<object>))]
    [SwaggerResponse(401, "JWT ausente ou inválido", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> Paineis(
        [FromQuery, SwaggerParameter(Description = "powerbi ou api")] string? tipo,
        [FromQuery, SwaggerParameter(Description = "1 = ativo, 0 = inativo")] string? ativo,
        [FromQuery, SwaggerParameter(Description = "CODPACOTECOMERCIAL (1 Atendimento, 2 Faturamento)")] int? codpacote,
        CancellationToken ct) =>
        _api.GetPaineisAsync(tipo, ativo, codpacote, ct);

    [HttpGet("apiconteudos/v1/paineis/{codpainel:int}/download")]
    [SwaggerOperation(
        Summary = "Download PBIX do painel",
        Description = "Baixa arquivo .pbix cadastrado no painel.",
        Tags = new[] { "Painéis" })]
    [SwaggerResponse(200, "Arquivo PBIX (application/octet-stream)")]
    [SwaggerResponse(401, "JWT ausente ou inválido", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Painel ou PBIX não encontrado", typeof(ResourceNotFoundDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> DownloadPainel(
        [SwaggerParameter(Description = "CODPAINEL", Required = true)] int codpainel,
        CancellationToken ct) =>
        _api.DownloadPainelAsync(codpainel, ct);

    [HttpPut("apiconteudos/v1/normalidades/{codnormalidade:int}")]
    [HttpPut("api/v1/normalidades/{codnormalidade:int}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Atualizar normalidade",
        Description = "Atualiza valor_min, valor_max, sexo e faixa etária de uma normalidade.",
        Tags = new[] { "Normalidades" })]
    [SwaggerResponse(200, "Normalidade atualizada", typeof(ApiMessageDoc))]
    [SwaggerResponse(400, "Campos obrigatórios ausentes", typeof(ApiErrorDoc))]
    [SwaggerResponse(401, "JWT ausente ou inválido (somente /apiconteudos/v1)", typeof(ApiErrorDoc))]
    [SwaggerResponse(404, "Normalidade não encontrada", typeof(ApiErrorDoc))]
    [SwaggerResponse(500, "Erro interno do servidor", typeof(ApiErrorDoc))]
    public Task<IActionResult> UpdateNormalidade(
        [SwaggerParameter(Description = "CODNORMALIDADE", Required = true)] int codnormalidade,
        [FromBody] UpdateNormalidadeRequest body,
        CancellationToken ct) =>
        _api.UpdateNormalidadeAsync(codnormalidade, body, ct);
}

[SwaggerSchema(Description = "Corpo para POST /token")]
public record TokenRequest(
    [property: SwaggerSchema(Description = "Senha de parceiro (API_JWT_PASSWORD)")]
    string? Senha,
    [property: SwaggerSchema(Description = "Alias em inglês para senha")]
    string? Password);

[SwaggerSchema(Description = "Corpo para PUT /normalidades/{id}")]
public class UpdateNormalidadeRequest
{
    [SwaggerSchema(Description = "Valor mínimo (obrigatório)")]
    [System.Text.Json.Serialization.JsonPropertyName("valor_min")]
    public decimal? ValorMin { get; set; }

    [SwaggerSchema(Description = "Valor máximo (obrigatório)")]
    [System.Text.Json.Serialization.JsonPropertyName("valor_max")]
    public decimal? ValorMax { get; set; }

    [SwaggerSchema(Description = "Sexo (M/F/U)")]
    [System.Text.Json.Serialization.JsonPropertyName("sexo")]
    public string? Sexo { get; set; }

    [SwaggerSchema(Description = "Idade mínima em anos")]
    [System.Text.Json.Serialization.JsonPropertyName("idade_min")]
    public int? IdadeMin { get; set; }

    [SwaggerSchema(Description = "Idade máxima em anos")]
    [System.Text.Json.Serialization.JsonPropertyName("idade_max")]
    public int? IdadeMax { get; set; }
}
