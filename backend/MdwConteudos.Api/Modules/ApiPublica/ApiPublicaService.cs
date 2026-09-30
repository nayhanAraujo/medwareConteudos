using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.ApiPublica;

public interface IApiPublicaService
{
    Task<IActionResult> ObterTokenAsync(TokenRequest? body, CancellationToken ct);
    Task<IActionResult> HealthAsync(CancellationToken ct);
    Task<IActionResult> GetVariaveisAsync(string? especialidade, string? unidade, CancellationToken ct);
    Task<IActionResult> GetVariavelDetalhadaAsync(int codvariavel, CancellationToken ct);
    Task<IActionResult> GetNormalidadesAsync(string? variavel, string? sexo, int? referencia, CancellationToken ct);
    Task<IActionResult> GetEcodopplerAsync(int referencia, CancellationToken ct);
    Task<IActionResult> GetFormulasAsync(CancellationToken ct);
    Task<IActionResult> GetReferenciasAsync(int? ano, string? especialidade, string? autor, CancellationToken ct);
    Task<IActionResult> GetSistemaInfoAsync(CancellationToken ct);
    Task<IActionResult> GetEspecialidadesAsync(CancellationToken ct);
    Task<IActionResult> GetRelatoriosAsync(string? ativo, string? multiselecao, string? nome, string? modulo, string? formato, int? codsistema, CancellationToken ct);
    Task<IActionResult> DownloadRelatorioAsync(int codrelatorio, CancellationToken ct);
    Task<IActionResult> GetScriptsAsync(string? sistema, string? aprovado, string? ativo, string? pacote, string? incluirArquivos, CancellationToken ct);
    Task<IActionResult> GetScriptUltimoVerificadoAsync(string? sistema, string? aprovado, string? ativo, string? pacote, string? incluirArquivos, CancellationToken ct);
    Task<IActionResult> GetScriptImagemAsync(int codscriptlaudo, int indice, CancellationToken ct);
    Task<IActionResult> DownloadScriptAsync(int codscriptlaudo, string? tipo, CancellationToken ct);
    Task<IActionResult> GetPaineisAsync(string? tipo, string? ativo, int? codpacote, CancellationToken ct);
    Task<IActionResult> DownloadPainelAsync(int codpainel, CancellationToken ct);
    Task<IActionResult> UpdateNormalidadeAsync(int codnormalidade, UpdateNormalidadeRequest body, CancellationToken ct);
    Task<IActionResult> GetClienteNormalidadesAsync(string clienteKey, string? padrao, CancellationToken ct);
    Task<IActionResult> GetClientePadroesNormalidadeAsync(string clienteKey, CancellationToken ct);
}

public class ApiPublicaService : IApiPublicaService
{
    private readonly IFirebirdConnectionFactory _db;
    private readonly ApiPartnerOptions _opts;
    private readonly ApiScriptOperations _scripts;
    private readonly TimeProvider _time;

    public ApiPublicaService(IFirebirdConnectionFactory db, IOptions<ApiPartnerOptions> opts, ApiScriptOperations scripts, TimeProvider? timeProvider = null)
    {
        _db = db;
        _opts = opts.Value;
        _scripts = scripts;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<IActionResult> ObterTokenAsync(TokenRequest? body, CancellationToken ct)
    {
        var senha = string.IsNullOrEmpty(body?.Senha) ? body?.Password : body.Senha;
        if (string.IsNullOrEmpty(senha))
            return new BadRequestObjectResult(new { success = false, error = "Senha não informada", message = "Envie um JSON com o campo \"senha\"" });
        if (string.IsNullOrEmpty(_opts.JwtPassword))
            return new ObjectResult(new { success = false, error = "Configuração do servidor", message = "Autenticação da API não configurada" }) { StatusCode = 503 };
        if (senha != _opts.JwtPassword)
            return new UnauthorizedObjectResult(new { success = false, error = "Não autorizado", message = "Senha inválida" });

        var datahora = _time.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var jwt = ApiJwtKeyHelper.CreateToken(new { senha = _opts.JwtPassword, datahora }, _opts.JwtSecret);
        return new OkObjectResult(new
        {
            success = true,
            token = jwt,
            expires_info = $"Válido no dia atual (UTC) e por até {_opts.JwtDatetimeToleranceHours.ToString(CultureInfo.InvariantCulture)}h. Gere um novo token quando necessário."
        });
    }

    public async Task<IActionResult> HealthAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await conn.ExecuteScalarAsync<int>("SELECT 1 FROM RDB$DATABASE");
            return new OkObjectResult(new { success = true, status = "healthy", database = "connected", timestamp = ApiV1Contract.DateTimeIso(DateTime.Now) });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { success = false, status = "unhealthy", database = "disconnected", error = ex.Message, timestamp = ApiV1Contract.DateTimeIso(DateTime.Now) }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetVariaveisAsync(string? especialidade, string? unidade, CancellationToken ct)
    {
        try
        {
            var sql = @"
                SELECT v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA, v.ABREVIACAO, v.DESCRICAO, v.CASASDECIMAIS,
                       u.DESCRICAO as UNIDADE_MEDIDA, LIST(e.NOME, ', ') as ESPECIALIDADES,
                       (SELECT LIST(nc.NOME, '|') FROM VARIAVEISNOMESCLINICOS nc WHERE nc.CODVARIAVEL = v.CODVARIAVEL) as NOMES_CLINICOS,
                       (SELECT LIST(a.ALTERNATIVA, '|') FROM VARIAVEISALTERNATIVAS a WHERE a.CODVARIAVEL = v.CODVARIAVEL) as ALTERNATIVAS
                FROM VARIAVEIS v
                LEFT JOIN UNIDADEMEDIDA u ON v.CODUNIDADEMEDIDA = u.CODUNIDADEMEDIDA
                LEFT JOIN VARIAVEL_ESPECIALIDADE ve ON v.CODVARIAVEL = ve.CODVARIAVEL
                LEFT JOIN ESPECIALIDADE e ON ve.CODESPECIALIDADE = e.CODESPECIALIDADE
                WHERE 1=1";
            var p = new DynamicParameters();
            if (!string.IsNullOrEmpty(especialidade)) { sql += " AND e.NOME = @especialidade"; p.Add("especialidade", especialidade); }
            if (!string.IsNullOrEmpty(unidade)) { sql += " AND u.DESCRICAO = @unidade"; p.Add("unidade", unidade); }
            sql += @" GROUP BY v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA, v.ABREVIACAO, v.DESCRICAO, v.CASASDECIMAIS, u.DESCRICAO ORDER BY v.NOME";

            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(sql, p);
            var list = rows.Select(r => new Dictionary<string, object?>
            {
                ["codvariavel"] = (int)r.CODVARIAVEL,
                ["nome"] = (string?)r.NOME,
                ["variavel"] = (string?)r.VARIAVEL,
                ["sigla"] = (string?)r.SIGLA,
                ["abreviacao"] = (string?)r.ABREVIACAO,
                ["descricao"] = (string?)r.DESCRICAO,
                ["casas_decimais"] = r.CASASDECIMAIS,
                ["unidade_medida"] = (string?)r.UNIDADE_MEDIDA,
                ["especialidades"] = ((string?)r.ESPECIALIDADES)?.Split(", ", StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
                ["nomes_clinicos"] = ((string?)r.NOMES_CLINICOS)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                ["alternativas"] = ((string?)r.ALTERNATIVAS)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>()
            }).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetVariavelDetalhadaAsync(int codvariavel, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var v = await conn.QueryFirstOrDefaultAsync(@"
                SELECT v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA, v.ABREVIACAO, v.DESCRICAO, v.CASASDECIMAIS,
                       u.DESCRICAO as UNIDADE_MEDIDA, LIST(e.NOME, ', ') as ESPECIALIDADES
                FROM VARIAVEIS v
                LEFT JOIN UNIDADEMEDIDA u ON v.CODUNIDADEMEDIDA = u.CODUNIDADEMEDIDA
                LEFT JOIN VARIAVEL_ESPECIALIDADE ve ON v.CODVARIAVEL = ve.CODVARIAVEL
                LEFT JOIN ESPECIALIDADE e ON ve.CODESPECIALIDADE = e.CODESPECIALIDADE
                WHERE v.CODVARIAVEL = @codvariavel
                GROUP BY v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA, v.ABREVIACAO, v.DESCRICAO, v.CASASDECIMAIS, u.DESCRICAO",
                new { codvariavel });
            if (v is null)
            {
                var exemplo = await conn.QueryAsync("SELECT CODVARIAVEL, NOME FROM VARIAVEIS ORDER BY CODVARIAVEL ROWS 10");
                return new NotFoundObjectResult(new
                {
                    success = false, error = "Variável não encontrada", codvariavel_buscado = codvariavel,
                    dica = "Confira se o servidor está usando o banco correto (APP_ENV e FIREBIRD_DB). Use GET /api/v1/variaveis para listar variáveis disponíveis.",
                    variaveis_exemplo = exemplo.Select(row => new { codvariavel = (int)row.CODVARIAVEL, nome = (string?)row.NOME }).ToList()
                });
            }

            var variavel = new Dictionary<string, object?>
            {
                ["codvariavel"] = (int)v.CODVARIAVEL, ["nome"] = (string?)v.NOME, ["variavel"] = (string?)v.VARIAVEL,
                ["sigla"] = (string?)v.SIGLA, ["abreviacao"] = (string?)v.ABREVIACAO, ["descricao"] = (string?)v.DESCRICAO,
                ["casas_decimais"] = v.CASASDECIMAIS, ["unidade_medida"] = (string?)v.UNIDADE_MEDIDA,
                ["especialidades"] = ((string?)v.ESPECIALIDADES)?.Split(", ", StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()
            };

            var comentarios = new Dictionary<int, List<(string Sexo, int IdadeMin, int IdadeMax, string? Texto)>>();
            foreach (var row in await conn.QueryAsync(
                         @"SELECT CODREFERENCIA, SEXO, IDADE_MIN, IDADE_MAX, TEXTO FROM NORMALIDADECOMENTARIO WHERE CODVARIAVEL = @codvariavel",
                         new { codvariavel }))
            {
                if (row.CODREFERENCIA is null) continue;
                var codRef = (int)row.CODREFERENCIA;
                var sexo = ((string?)row.SEXO ?? "A").Trim().ToUpperInvariant();
                if (sexo is not ("F" or "M" or "A")) sexo = "A";
                var idadeMin = row.IDADE_MIN is null ? -1 : (int)row.IDADE_MIN;
                var idadeMax = row.IDADE_MAX is null ? -1 : (int)row.IDADE_MAX;
                if (idadeMin < 0) idadeMin = -1;
                if (idadeMax < 0) idadeMax = -1;
                if (!comentarios.TryGetValue(codRef, out var list))
                {
                    list = [];
                    comentarios[codRef] = list;
                }
                list.Add((sexo, idadeMin, idadeMax, Iso88591SafeText.ForDisplay((string?)row.TEXTO)));
            }

            var normalidades = (await conn.QueryAsync(@"
                SELECT n.CODNORMALIDADE, n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX,
                       r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO,
                       LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS AUTORES,
                       c.NOME AS CLASSIFICACAO
                FROM NORMALIDADE n
                LEFT JOIN REFERENCIA r ON n.CODREFERENCIA = r.CODREFERENCIA
                LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
                LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
                LEFT JOIN CLASSIFICACOES c ON n.CODCLASSIFICACAO = c.CODCLASSIFICACAO
                WHERE n.CODVARIAVEL = @codvariavel
                GROUP BY n.CODNORMALIDADE, n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX,
                         r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO, c.NOME ORDER BY r.ANO DESC NULLS LAST",
                new { codvariavel })).Select(r => MapNormalidadeRow(r, comentarios)).ToList();

            var alternativas = (await conn.QueryAsync<string>(
                "SELECT ALTERNATIVA FROM VARIAVEISALTERNATIVAS WHERE CODVARIAVEL = @codvariavel ORDER BY ALTERNATIVA",
                new { codvariavel })).ToList();

            var formulasRaw = await conn.QueryAsync(@"
                SELECT f.CODFORMULA, f.CODVARIAVEL, f.FORMULA, f.CASADECIMAIS, el.CODEQUACAO, el.EQUACAO, tl.NOME AS LINGUAGEM,
                       r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO,
                       LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS AUTORES
                FROM FORMULAS f
                LEFT JOIN FORMULA_VARIAVEL fv ON fv.CODFORMULA = f.CODFORMULA AND fv.CODVARIAVEL = @codvariavel
                LEFT JOIN EQUACOESLINGUAGEM el ON f.CODFORMULA = el.CODFORMULA
                LEFT JOIN TIPOLINGUAGEM tl ON el.CODLINGUAGEM = tl.CODLINGUAGEM
                LEFT JOIN REFERENCIA r ON el.CODREFERENCIA = r.CODREFERENCIA
                LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
                LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
                WHERE f.CODVARIAVEL = @codvariavel OR fv.CODVARIAVEL = @codvariavel
                GROUP BY f.CODFORMULA, f.CODVARIAVEL, f.FORMULA, f.CASADECIMAIS, el.CODEQUACAO, el.EQUACAO,
                         tl.NOME, r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO
                ORDER BY r.ANO DESC NULLS LAST", new { codvariavel });

            var formulas = formulasRaw.Select(r => new
            {
                codformula = (int)r.CODFORMULA,
                codvariavel = r.CODVARIAVEL is int cv ? cv : r.CODVARIAVEL is long cl ? (int)cl : (int?)null,
                formula = (string?)r.FORMULA,
                casas_decimais = r.CASADECIMAIS,
                equacoes = r.CODEQUACAO is null ? Array.Empty<object>() : new object[]
                {
                    new
                    {
                        codequacao = (int?)r.CODEQUACAO,
                        equacao = (string?)r.EQUACAO,
                        linguagem = (string?)r.LINGUAGEM,
                        referencia = r.CODREFERENCIA is null ? null : new
                        {
                            codigo = (int?)r.CODREFERENCIA,
                            titulo = (string?)r.TITULO,
                            ano = r.ANO,
                            descricao = (string?)r.DESCRICAO,
                            autores = (string?)r.AUTORES
                        }
                    }
                }
            }).ToList();

            return new OkObjectResult(new
            {
                success = true,
                data = new
                {
                    variavel,
                    normalidades,
                    formulas,
                    alternativas,
                    comentarios = comentarios.SelectMany(kv =>
                        kv.Value.Select(s => new
                        {
                            codigo = kv.Key,
                            sexo = s.Sexo,
                            idade_min = s.IdadeMin,
                            idade_max = s.IdadeMax,
                            texto = s.Texto
                        }))
                },
                timestamp = ApiV1Contract.DateTimeIso(DateTime.Now)
            });
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetNormalidadesAsync(string? variavel, string? sexo, int? referencia, CancellationToken ct)
    {
        try
        {
            var sql = @"
                SELECT n.CODNORMALIDADE, n.CODVARIAVEL, v.NOME as NOME_VARIAVEL, v.SIGLA as SIGLA_VARIAVEL,
                       n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX,
                       r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO,
                       LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS AUTORES
                FROM NORMALIDADE n
                JOIN VARIAVEIS v ON n.CODVARIAVEL = v.CODVARIAVEL
                LEFT JOIN REFERENCIA r ON n.CODREFERENCIA = r.CODREFERENCIA
                LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
                LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR WHERE 1=1";
            var p = new DynamicParameters();
            if (!string.IsNullOrEmpty(variavel))
            {
                if (int.TryParse(variavel, out var cod)) { sql += " AND n.CODVARIAVEL = @codVar"; p.Add("codVar", cod); }
                else { sql += " AND v.VARIAVEL = @nomeVar"; p.Add("nomeVar", variavel); }
            }
            if (!string.IsNullOrEmpty(sexo)) { sql += " AND n.SEXO = @sexo"; p.Add("sexo", sexo); }
            if (referencia.HasValue) { sql += " AND n.CODREFERENCIA = @ref"; p.Add("ref", referencia.Value); }
            sql += @" GROUP BY n.CODNORMALIDADE, n.CODVARIAVEL, v.NOME, v.SIGLA, n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX,
                      r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO ORDER BY v.NOME, n.SEXO";

            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(sql, p);
            var list = rows.Select(MapNormalidadeListRow).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetEcodopplerAsync(int referencia, CancellationToken ct)
    {
        if (referencia < 1)
            return new BadRequestObjectResult(new { success = false, error = "Parâmetro referencia inválido", message = "Informe um código de referência inteiro ≥ 1." });
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var exists = await conn.ExecuteScalarAsync<int?>("SELECT 1 FROM REFERENCIA WHERE CODREFERENCIA = @referencia", new { referencia });
            if (exists != 1)
                return new NotFoundObjectResult(new { success = false, error = "Referência não encontrada", message = $"Não existe referência com CODREFERENCIA = {referencia}." });

            var rows = await conn.QueryAsync(@"
                SELECT v.VARIAVEL, n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX, n.PAGINA_REFERENCIA,
                       c.NOME AS CLASSIFICACAO, r.TITULO AS REFERENCIA_TITULO, r.ANO AS REFERENCIA_ANO
                FROM NORMALIDADE n
                JOIN VARIAVEIS v ON n.CODVARIAVEL = v.CODVARIAVEL
                LEFT JOIN CLASSIFICACOES c ON n.CODCLASSIFICACAO = c.CODCLASSIFICACAO
                LEFT JOIN REFERENCIA r ON n.CODREFERENCIA = r.CODREFERENCIA
                WHERE n.CODREFERENCIA = @referencia ORDER BY v.VARIAVEL, n.SEXO, n.VALORMIN",
                new { referencia });

            var resultado = EcodopplerBuilder.Build(rows);
            return new OkObjectResult(resultado);
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetFormulasAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(@"
                SELECT v.VARIAVEL, el.NOME_FUNCAO, el.EQUACAO, tl.NOME AS LINGUAGEM
                FROM FORMULAS f
                LEFT JOIN FORMULA_VARIAVEL fv ON fv.CODFORMULA = f.CODFORMULA
                JOIN VARIAVEIS v ON v.CODVARIAVEL = COALESCE(fv.CODVARIAVEL, f.CODVARIAVEL)
                JOIN EQUACOESLINGUAGEM el ON el.CODFORMULA = f.CODFORMULA
                LEFT JOIN TIPOLINGUAGEM tl ON tl.CODLINGUAGEM = el.CODLINGUAGEM ORDER BY v.VARIAVEL");
            var dict = new Dictionary<string, List<object>>();
            foreach (var row in rows)
            {
                var key = (string)row.VARIAVEL;
                if (!dict.ContainsKey(key)) dict[key] = new List<object>();
                dict[key].Add(new { nome_funcao = row.NOME_FUNCAO ?? "", equacao = row.EQUACAO ?? "", linguagem = row.LINGUAGEM ?? "" });
            }
            return new OkObjectResult(dict);
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetReferenciasAsync(int? ano, string? especialidade, string? autor, CancellationToken ct)
    {
        try
        {
            var sql = @"
                SELECT r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO, e.NOME as ESPECIALIDADE,
                       LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS AUTORES
                FROM REFERENCIA r
                LEFT JOIN ESPECIALIDADE e ON r.CODESPECIALIDADE = e.CODESPECIALIDADE
                LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
                LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR WHERE 1=1";
            var p = new DynamicParameters();
            if (ano.HasValue) { sql += " AND r.ANO = @ano"; p.Add("ano", ano); }
            if (!string.IsNullOrEmpty(especialidade)) { sql += " AND e.NOME = @esp"; p.Add("esp", especialidade); }
            if (!string.IsNullOrEmpty(autor)) { sql += " AND a.NOME LIKE @autor"; p.Add("autor", $"%{autor}%"); }
            sql += " GROUP BY r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO, e.NOME ORDER BY r.ANO DESC, r.TITULO";

            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(sql, p);
            var list = rows.Select(r => new { codigo = (int)r.CODREFERENCIA, titulo = (string?)r.TITULO, ano = r.ANO, descricao = (string?)r.DESCRICAO, especialidade = (string?)r.ESPECIALIDADE, autores = (string?)r.AUTORES }).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetSistemaInfoAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var info = new
            {
                estatisticas = new
                {
                    total_variaveis = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS"),
                    total_normalidades = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM NORMALIDADE"),
                    total_referencias = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM REFERENCIA"),
                    total_autores = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AUTORES"),
                    total_especialidades = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ESPECIALIDADE"),
                    variaveis_com_normalidade = await conn.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT CODVARIAVEL) FROM NORMALIDADE"),
                    variaveis_com_formula = await conn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(DISTINCT CODVARIAVEL) FROM FORMULA_VARIAVEL")
                },
                variaveis_por_especialidade = ApiV1Contract.SistemaEspecialidades(await conn.QueryAsync(@"
                    SELECT e.NOME as especialidade, COUNT(DISTINCT v.CODVARIAVEL) as total
                    FROM ESPECIALIDADE e
                    LEFT JOIN VARIAVEL_ESPECIALIDADE ve ON e.CODESPECIALIDADE = ve.CODESPECIALIDADE
                    LEFT JOIN VARIAVEIS v ON ve.CODVARIAVEL = v.CODVARIAVEL
                    GROUP BY e.NOME ORDER BY total DESC")),
                versao_api = "1.0",
                ultima_atualizacao = ApiV1Contract.DateTimeIso(DateTime.Now)
            };
            return new OkObjectResult(ApiV1Contract.Ok(info));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetEspecialidadesAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(@"
                SELECT e.CODESPECIALIDADE, e.NOME, e.DESCRICAO, COUNT(DISTINCT ve.CODVARIAVEL) as TOTAL_VARIAVEIS
                FROM ESPECIALIDADE e
                LEFT JOIN VARIAVEL_ESPECIALIDADE ve ON e.CODESPECIALIDADE = ve.CODESPECIALIDADE
                GROUP BY e.CODESPECIALIDADE, e.NOME, e.DESCRICAO ORDER BY e.NOME");
            var list = rows.Select(r => new { codigo = (int)r.CODESPECIALIDADE, nome = (string?)r.NOME, descricao = (string?)r.DESCRICAO, total_variaveis = Convert.ToInt32(r.TOTAL_VARIAVEIS) }).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetRelatoriosAsync(string? ativo, string? multiselecao, string? nome, string? modulo, string? formato, int? codsistema, CancellationToken ct)
    {
        try
        {
            ativo = ativo?.Trim();
            multiselecao = multiselecao?.Trim();
            nome = nome?.Trim();
            modulo = modulo?.Trim();
            formato = formato?.Trim();
            if (!string.IsNullOrEmpty(ativo) && ativo is not "0" and not "1")
                return new BadRequestObjectResult(new { success = false, error = "Parâmetro inválido", message = "Use ativo=1 para relatórios ativos ou ativo=0 para inativos" });

            int? msVal = null;
            if (!string.IsNullOrEmpty(multiselecao))
            {
                if (multiselecao is not "0" and not "1") return new BadRequestObjectResult(new { success = false, error = "Parâmetro inválido", message = "Use multiselecao=1 (com multiseleção) ou multiselecao=0 (sem)" });
                msVal = int.Parse(multiselecao);
            }

            var where = new List<string> { "1=1" };
            var p = new DynamicParameters();
            if (ativo is "0" or "1") { where.Add("ATIVO = @ativo"); p.Add("ativo", int.Parse(ativo)); }
            if (msVal.HasValue) { where.Add("TEM_MULTISELECAO = @ms"); p.Add("ms", msVal); }
            if (!string.IsNullOrEmpty(nome)) { where.Add("(UPPER(NOME) LIKE UPPER(@nome) OR UPPER(MODULO) LIKE UPPER(@nome))"); p.Add("nome", $"%{nome}%"); }
            if (!string.IsNullOrEmpty(modulo)) { where.Add("MODULO = @modulo"); p.Add("modulo", modulo); }
            if (!string.IsNullOrEmpty(formato)) { where.Add("FORMATO = @formato"); p.Add("formato", formato.ToUpperInvariant()); }
            if (codsistema.HasValue) { where.Add("CODMODULO IN (SELECT SM.CODMODULO FROM SISTEMA_MODULO SM WHERE SM.CODSISTEMA = @cs)"); p.Add("cs", codsistema); }

            var sql = $@"SELECT r.CODRELATORIO, r.NOME, r.MODULO, r.FORMATO, r.DTHRCRIACAO, r.ATIVO, r.TEM_MULTISELECAO
                         FROM RELATORIOS r JOIN RELATORIOVALIDACOES rv ON rv.CODRELATORIO = r.CODRELATORIO
                         WHERE {string.Join(" AND ", where)} ORDER BY r.DTHRCRIACAO DESC";
            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(sql, p);
            var list = rows.Select(r => new
            {
                codrelatorio = (int)r.CODRELATORIO,
                nome = (string?)r.NOME,
                modulo = (string?)r.MODULO,
                formato = (string?)r.FORMATO,
                dthrcriacao = ApiV1Contract.FormatDate((object?)r.DTHRCRIACAO),
                ativo = r.ATIVO,
                tem_multiselecao = Convert.ToBoolean(r.TEM_MULTISELECAO)
            }).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> DownloadRelatorioAsync(int codrelatorio, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var row = await conn.QueryFirstOrDefaultAsync("SELECT NOME, FORMATO, CONTEUDO FROM RELATORIOS WHERE CODRELATORIO = @id", new { id = codrelatorio });
            if (row is null) return new NotFoundObjectResult(new { success = false, error = "Relatório não encontrado", codrelatorio });
            var bytes = BlobHelper.ToBytes(row.CONTEUDO) ?? Array.Empty<byte>();
            var filename = ApiV1Contract.RelatorioFileName((string?)row.NOME, (string?)row.FORMATO, codrelatorio);
            return new FileContentResult(bytes, "application/octet-stream; charset=utf-8") { FileDownloadName = filename };
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetScriptsAsync(string? sistema, string? aprovado, string? ativo, string? pacote, string? incluirArquivos, CancellationToken ct)
    {
        try
        {
            var incluir = ApiScriptOperations.ParseIncluirArquivos(incluirArquivos);
            var list = await _scripts.ListScriptsAsync(sistema, aprovado, ativo, pacote, incluir, ct);
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetScriptUltimoVerificadoAsync(
        string? sistema, string? aprovado, string? ativo, string? pacote, string? incluirArquivos, CancellationToken ct)
    {
        try
        {
            var incluir = ApiScriptOperations.ParseIncluirArquivos(incluirArquivos);
            var (item, workflowKey) = await _scripts.GetUltimoVerificadoAsync(sistema, aprovado, ativo, pacote, incluir, ct);
            if (item is null) return new StatusCodeResult(204);
            return new OkObjectResult(new
            {
                success = true,
                data = item,
                workflow_key = workflowKey,
                timestamp = ApiV1Contract.DateTimeIso(DateTime.Now)
            });
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetScriptImagemAsync(int codscriptlaudo, int indice, CancellationToken ct)
    {
        try
        {
            var (path, meta) = await _scripts.ResolveScriptImagemAsync(codscriptlaudo, indice, ct);
            if (meta is null)
                return new NotFoundObjectResult(new { success = false, error = "Script não encontrado", codscriptlaudo });
            if (path is null)
                return new NotFoundObjectResult(new { success = false, error = "Imagem não encontrada", codscriptlaudo, indice, meta });
            var mime = GetMimeType(path);
            meta.TryGetValue("nome", out var imgNome);
            if (string.IsNullOrWhiteSpace(imgNome)) imgNome = Path.GetFileName(path);
            return new PhysicalFileResult(path, mime) { FileDownloadName = imgNome, EnableRangeProcessing = true };
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public Task<IActionResult> DownloadScriptAsync(int codscriptlaudo, string? tipo, CancellationToken ct) =>
        _scripts.DownloadScriptAsync(codscriptlaudo, tipo, ct);

    public async Task<IActionResult> GetPaineisAsync(string? tipo, string? ativo, int? codpacote, CancellationToken ct)
    {
        try
        {
            var tipoRaw = (tipo ?? "").Trim().ToLowerInvariant();
            var ativoRaw = (ativo ?? "").Trim();
            if (!string.IsNullOrEmpty(tipoRaw) && tipoRaw is not ("powerbi" or "api"))
                return new BadRequestObjectResult(new { success = false, error = "Parâmetro inválido", message = "Use tipo=powerbi ou tipo=api" });
            if (!string.IsNullOrEmpty(ativoRaw) && ativoRaw is not ("0" or "1"))
                return new BadRequestObjectResult(new { success = false, error = "Parâmetro inválido", message = "Use ativo=1 ou ativo=0" });

            var fromClause = @"
                FROM Paineis p
                LEFT JOIN Clientes c ON p.CODCLIENTE = c.CODCLIENTE
                LEFT JOIN ModulosSistema m ON p.CODMODULO = m.CODMODULO";
            if (codpacote.HasValue)
                fromClause += " INNER JOIN Paineis_Pacotes pp ON p.CODPAINEL = pp.CODPAINEL";

            var where = new List<string>();
            var p = new DynamicParameters();
            if (!string.IsNullOrEmpty(tipoRaw)) { where.Add("p.TIPO_PAINEL = @tipo"); p.Add("tipo", tipoRaw.ToUpperInvariant()); }
            if (ativoRaw is "0" or "1") { where.Add("p.ATIVO = @ativo"); p.Add("ativo", int.Parse(ativoRaw)); }
            if (codpacote.HasValue) { where.Add("pp.CODPACOTECOMERCIAL = @codpacote"); p.Add("codpacote", codpacote.Value); }
            var whereClause = where.Count > 0 ? string.Join(" AND ", where) : "1=1";

            var sql = $@"
                SELECT DISTINCT p.CODPAINEL, p.NOME, p.DESCRICAO, p.ATIVO, p.TIPO_PAINEL,
                       p.CODCLIENTE, c.NOME AS NOME_CLIENTE, p.CODMODULO, m.NOME AS NOME_MODULO,
                       CASE WHEN p.ARQUIVO_PBIX IS NOT NULL THEN 1 ELSE 0 END AS TEM_PBIX,
                       p.NOME_ARQUIVO_PBIX
                {fromClause}
                WHERE {whereClause}
                ORDER BY p.NOME";

            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = (await conn.QueryAsync(sql, p)).ToList();
            var paineis = new List<object>();
            foreach (var r in rows)
            {
                var codpainel = (int)r.CODPAINEL;
                var pacotes = (await conn.QueryAsync<string>(@"
                    SELECT pc.NOME FROM Paineis_Pacotes pp
                    JOIN PacotesComerciais pc ON pp.CODPACOTECOMERCIAL = pc.CODPACOTECOMERCIAL
                    WHERE pp.CODPAINEL = @id ORDER BY pc.NOME", new { id = codpainel })).ToList();
                paineis.Add(new
                {
                    codpainel,
                    nome = (string?)r.NOME,
                    descricao = (string?)r.DESCRICAO,
                    ativo = r.ATIVO,
                    tipo_painel = (string?)r.TIPO_PAINEL,
                    codcliente = r.CODCLIENTE,
                    nome_cliente = (string?)r.NOME_CLIENTE,
                    codmodulo = r.CODMODULO,
                    nome_modulo = (string?)r.NOME_MODULO,
                    tem_arquivo_pbix = Convert.ToInt32(r.TEM_PBIX) == 1,
                    nome_arquivo_pbix = (string?)r.NOME_ARQUIVO_PBIX,
                    pacotes
                });
            }
            return new OkObjectResult(ApiV1Contract.Ok(paineis, paineis.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> DownloadPainelAsync(int codpainel, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var row = await conn.QueryFirstOrDefaultAsync(
                "SELECT NOME, NOME_ARQUIVO_PBIX, ARQUIVO_PBIX FROM Paineis WHERE CODPAINEL = @id", new { id = codpainel });
            if (row is null)
                return new NotFoundObjectResult(new { success = false, error = "Painel não encontrado", codpainel });
            byte[]? bytes = BlobHelper.ToBytes((object?)row.ARQUIVO_PBIX);
            if (bytes is null || bytes.Length == 0)
                return new NotFoundObjectResult(new { success = false, error = "Arquivo PBIX não disponível", codpainel });
            var downloadName = SanitizePainelFilename((string?)row.NOME_ARQUIVO_PBIX ?? (string?)row.NOME, codpainel);
            return new FileContentResult(bytes, "application/octet-stream") { FileDownloadName = downloadName };
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> UpdateNormalidadeAsync(int codnormalidade, UpdateNormalidadeRequest body, CancellationToken ct)
    {
        if (body.ValorMin is null || body.ValorMax is null)
            return new BadRequestObjectResult(new { success = false, error = "valor_min e valor_max são obrigatórios" });
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.ExecuteAsync(@"
                UPDATE NORMALIDADE SET VALORMIN=@vmin, VALORMAX=@vmax, SEXO=@sexo, IDADE_MIN=@imin, IDADE_MAX=@imax,
                DTHRULTMODIFICACAO = CURRENT_TIMESTAMP WHERE CODNORMALIDADE=@id",
                new { vmin = body.ValorMin, vmax = body.ValorMax, sexo = body.Sexo, imin = body.IdadeMin, imax = body.IdadeMax, id = codnormalidade });
            if (rows == 0) return new NotFoundObjectResult(new { success = false, error = "Normalidade não encontrada" });
            return new OkObjectResult(ApiV1Contract.OkMessage("Normalidade atualizada com sucesso"));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetClienteNormalidadesAsync(string clienteKey, string? padrao, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var codCliente = await ResolveClienteKeyAsync(conn, clienteKey);
            if (!codCliente.HasValue)
                return new NotFoundObjectResult(new { success = false, error = "Cliente não encontrado", message = $"Não existe cliente com identificador '{clienteKey}'." });

            var padraoRow = await ResolvePadraoAsync(conn, codCliente.Value, padrao);
            if (padraoRow is null)
                return new NotFoundObjectResult(new { success = false, error = "Padrão não encontrado", message = "Nenhum padrão vigente ou código informado." });

            var rows = await conn.QueryAsync(@"
                SELECT v.VARIAVEL, f.SEXO, f.VALORMIN, f.VALORMAX, f.IDADE_MIN, f.IDADE_MAX, f.PAGINA_REFERENCIA,
                       c.NOME AS CLASSIFICACAO, p.NOME AS PADRAO_NOME, p.CODIGO AS PADRAO_CODIGO
                FROM PADRAONORMALIDADEFAIXA f
                JOIN VARIAVEIS v ON f.CODVARIAVEL = v.CODVARIAVEL
                JOIN CLIENTESPADRAONORMALIDADE p ON p.CODPADRAO = f.CODPADRAO
                LEFT JOIN CLASSIFICACOES c ON f.CODCLASSIFICACAO = c.CODCLASSIFICACAO
                WHERE f.CODPADRAO = @codPadrao
                ORDER BY v.VARIAVEL, f.SEXO, f.VALORMIN",
                new { codPadrao = padraoRow.CodPadrao });

            var comentariosRows = await conn.QueryAsync<(string Variavel, string Sexo, string Texto)>(@"
                SELECT v.VARIAVEL, pc.SEXO, pc.TEXTO
                FROM PADRAONORMALIDADECOMENTARIO pc
                JOIN VARIAVEIS v ON v.CODVARIAVEL = pc.CODVARIAVEL
                WHERE pc.CODPADRAO = @codPadrao",
                new { codPadrao = padraoRow.CodPadrao });
            var comentarios = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (variavel, sexoRaw, texto) in comentariosRows)
            {
                var sexo = (sexoRaw ?? "A").Trim().ToUpperInvariant();
                if (sexo is not ("F" or "M" or "A")) sexo = "A";
                if (!comentarios.TryGetValue(variavel, out var porSexo))
                {
                    porSexo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    comentarios[variavel] = porSexo;
                }
                porSexo[sexo] = Iso88591SafeText.ForDisplay(texto);
            }

            var resultado = ClienteNormalidadesBuilder.Build(rows, comentarios);
            return new OkObjectResult(new
            {
                success = true,
                cliente = new { codigo = codCliente.Value },
                padrao = new
                {
                    codigo = padraoRow.CodPadrao,
                    codigo_api = padraoRow.Codigo,
                    nome = padraoRow.Nome,
                    vigente = padraoRow.PadraoVigente == 1
                },
                data = resultado
            });
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetClientePadroesNormalidadeAsync(string clienteKey, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var codCliente = await ResolveClienteKeyAsync(conn, clienteKey);
            if (!codCliente.HasValue)
                return new NotFoundObjectResult(new { success = false, error = "Cliente não encontrado" });

            var rows = await conn.QueryAsync(@"
                SELECT CODPADRAO, NOME, CODIGO, CODREFERENCIA, PADRAOVIGENTE, ATIVO, DESCRICAO
                FROM CLIENTESPADRAONORMALIDADE
                WHERE CODCLIENTE = @codCliente AND ATIVO = 1
                ORDER BY PADRAOVIGENTE DESC, NOME",
                new { codCliente });

            var list = rows.Select(r => new
            {
                codigo = (int)r.CODPADRAO,
                codigo_api = (string)r.CODIGO,
                nome = (string)r.NOME,
                cod_referencia_origem = (int?)r.CODREFERENCIA,
                padrao_vigente = (short)r.PADRAOVIGENTE == 1,
                descricao = (string?)r.DESCRICAO
            }).ToList();

            return new OkObjectResult(ApiResponse.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    private static async Task<int?> ResolveClienteKeyAsync(System.Data.Common.DbConnection conn, string clienteKey)
    {
        if (int.TryParse(clienteKey, out var cod))
        {
            var exists = await conn.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM Clientes WHERE CODCLIENTE = @cod", new { cod });
            return exists == 1 ? cod : null;
        }
        return null;
    }

    private static async Task<PadraoResolveRow?> ResolvePadraoAsync(
        System.Data.Common.DbConnection conn, int codCliente, string? padraoCodigo)
    {
        if (!string.IsNullOrWhiteSpace(padraoCodigo))
        {
            return await conn.QueryFirstOrDefaultAsync<PadraoResolveRow>(@"
                SELECT CODPADRAO AS CodPadrao, CODIGO AS Codigo, NOME AS Nome, PADRAOVIGENTE AS PadraoVigente
                FROM CLIENTESPADRAONORMALIDADE
                WHERE CODCLIENTE = @codCliente AND ATIVO = 1 AND UPPER(CODIGO) = @codigo",
                new { codCliente, codigo = padraoCodigo.Trim().ToUpperInvariant() });
        }

        return await conn.QueryFirstOrDefaultAsync<PadraoResolveRow>(@"
            SELECT CODPADRAO AS CodPadrao, CODIGO AS Codigo, NOME AS Nome, PADRAOVIGENTE AS PadraoVigente
            FROM CLIENTESPADRAONORMALIDADE
            WHERE CODCLIENTE = @codCliente AND ATIVO = 1 AND PADRAOVIGENTE = 1",
            new { codCliente });
    }

    private sealed class PadraoResolveRow
    {
        public int CodPadrao { get; set; }
        public string Codigo { get; set; } = "";
        public string Nome { get; set; } = "";
        public int PadraoVigente { get; set; }
    }

    private static object MapNormalidadeRow(
        dynamic row,
        IReadOnlyDictionary<int, List<(string Sexo, int IdadeMin, int IdadeMax, string? Texto)>>? comentarios = null
    )
    {
        string? comentarioTexto = null;
        if (row.CODREFERENCIA is not null && comentarios is not null
            && comentarios.TryGetValue((int)row.CODREFERENCIA, out var list))
        {
            var sexoFaixa = ((string?)row.SEXO ?? "A").Trim().ToUpperInvariant();
            int idadeMinFaixa = row.IDADE_MIN is null ? -1 : (int)row.IDADE_MIN;
            int idadeMaxFaixa = row.IDADE_MAX is null ? -1 : (int)row.IDADE_MAX;
            if (idadeMinFaixa < 0 && idadeMaxFaixa < 0)
            {
                idadeMinFaixa = -1;
                idadeMaxFaixa = -1;
            }

            comentarioTexto = ResolveComentarioPorSexoIdade(list, sexoFaixa, idadeMinFaixa, idadeMaxFaixa);
        }

        return new
        {
            codnormalidade = (int)row.CODNORMALIDADE,
            sexo = (string?)row.SEXO,
            valor_min = row.VALORMIN,
            valor_max = row.VALORMAX,
            idade_min = row.IDADE_MIN,
            idade_max = row.IDADE_MAX,
            classificacao = (string?)row.CLASSIFICACAO,
            comentario_texto = comentarioTexto,
            referencia = row.CODREFERENCIA is null ? null : new
            {
                codigo = (int?)row.CODREFERENCIA,
                titulo = (string?)row.TITULO,
                ano = row.ANO,
                descricao = (string?)row.DESCRICAO,
                autores = (string?)row.AUTORES
            }
        };
    }

    private static string? ResolveComentarioPorSexoIdade(
        IReadOnlyList<(string Sexo, int IdadeMin, int IdadeMax, string? Texto)> list,
        string sexoFaixa,
        int idadeMinFaixa,
        int idadeMaxFaixa
    )
    {
        static string? Pick(
            IReadOnlyList<(string Sexo, int IdadeMin, int IdadeMax, string? Texto)> src,
            string sexo,
            int imin,
            int imax
        )
        {
            var hit = src.FirstOrDefault(c =>
                c.Sexo.Equals(sexo, StringComparison.OrdinalIgnoreCase)
                && c.IdadeMin == imin
                && c.IdadeMax == imax
                && !string.IsNullOrWhiteSpace(c.Texto));
            return string.IsNullOrWhiteSpace(hit.Texto) ? null : hit.Texto;
        }

        var sexo = sexoFaixa is "F" or "M" ? sexoFaixa : "A";
        var hasAge = idadeMinFaixa >= 0 || idadeMaxFaixa >= 0;
        var imin = hasAge ? idadeMinFaixa : -1;
        var imax = hasAge ? idadeMaxFaixa : -1;

        if (hasAge)
        {
            var t = Pick(list, sexo, imin, imax);
            if (t is not null) return t;
            if (sexo is not "A")
            {
                t = Pick(list, "A", imin, imax);
                if (t is not null) return t;
            }
        }

        var t2 = Pick(list, sexo, -1, -1);
        if (t2 is not null) return t2;
        if (sexo is not "A")
        {
            t2 = Pick(list, "A", -1, -1);
            if (t2 is not null) return t2;
        }

        return list.Select(c => c.Texto).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static object MapNormalidadeListRow(dynamic row) => new
    {
        codnormalidade = (int)row.CODNORMALIDADE,
        variavel = new { codigo = (int)row.CODVARIAVEL, nome = (string?)row.NOME_VARIAVEL, sigla = (string?)row.SIGLA_VARIAVEL },
        sexo = (string?)row.SEXO,
        valor_min = row.VALORMIN,
        valor_max = row.VALORMAX,
        idade_min = row.IDADE_MIN,
        idade_max = row.IDADE_MAX,
        referencia = row.CODREFERENCIA is null ? null : new
        {
            codigo = (int?)row.CODREFERENCIA,
            titulo = (string?)row.TITULO,
            ano = row.ANO,
            descricao = (string?)row.DESCRICAO,
            autores = (string?)row.AUTORES
        }
    };

    private static ObjectResult Err500(Exception ex) =>
        new(new { success = false, error = "Erro interno do servidor", message = ex.Message }) { StatusCode = 500 };

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    private static string SanitizePainelFilename(string? nomeBase, int codpainel)
    {
        var sane = string.Concat((nomeBase ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')).Trim().Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(sane)) sane = $"painel_{codpainel}";
        if (!sane.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)) sane += ".pbix";
        return sane;
    }
}

public static class BlobHelper
{
    public static byte[]? ToBytes(object? blob)
    {
        if (blob is null or DBNull) return null;
        if (blob is byte[] b) return b;
        if (blob is string s) return Encoding.UTF8.GetBytes(s);
        return Encoding.UTF8.GetBytes(blob.ToString() ?? "");
    }
}

public static class EcodopplerBuilder
{
    // Keep the public Python mapping independent from modern client classifications.
    public static Dictionary<string, Dictionary<string, Dictionary<string, object>>> Build(IEnumerable<dynamic> rows)
    {
        var result = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
        foreach (var row in rows)
        {
            var variable = (string)row.VARIAVEL;
            var sex = string.IsNullOrEmpty((string?)row.SEXO) ? "U" : (string)row.SEXO;
            if (!result.TryGetValue(variable, out var sexes)) result[variable] = sexes = new();
            if (!sexes.TryGetValue(sex, out var zones)) sexes[sex] = zones = new();
            var classification = (string?)row.CLASSIFICACAO;
            // Unclassified rows keep the first default in the SQL ordering.
            if (string.IsNullOrEmpty(classification) && zones.ContainsKey("default")) continue;
            var meta = new Dictionary<string, object?>();
            if (row.PAGINA_REFERENCIA is not null && Convert.ToDouble(row.PAGINA_REFERENCIA) != 0)
                meta["Pagina"] = Convert.ToDouble(row.PAGINA_REFERENCIA);
            if (!string.IsNullOrEmpty((string?)row.REFERENCIA_TITULO)) meta["Fonte"] = (string)row.REFERENCIA_TITULO;
            if (row.REFERENCIA_ANO is not null && Convert.ToInt32(row.REFERENCIA_ANO) != 0) meta["Ano"] = row.REFERENCIA_ANO;
            zones[MapLegacyZone(classification)] = new Dictionary<string, object>
            {
                ["min"] = row.VALORMIN is null ? null! : Convert.ToDouble(row.VALORMIN),
                ["max"] = row.VALORMAX is null ? null! : Convert.ToDouble(row.VALORMAX),
                ["_meta"] = meta
            };
        }

        foreach (var sexes in result.Values)
        foreach (var zones in sexes.Values)
        {
            if (zones.Count != 1 || !zones.TryGetValue("default", out var value)
                || value is not Dictionary<string, object> range || range["min"] is null || range["max"] is null)
                continue;
            var min = Convert.ToDouble(range["min"]);
            var max = Convert.ToDouble(range["max"]);
            var originalMeta = (Dictionary<string, object?>)range["_meta"];
            // Python calculated quartiles inherit Fonte/Pagina, but never Ano.
            var meta = originalMeta.Where(pair => pair.Key != "Ano").ToDictionary(pair => pair.Key, pair => pair.Value);
            var names = new[] { "low", "moderated", "elevated", "high" };
            for (var index = 0; index < names.Length; index++)
                zones[names[index]] = new Dictionary<string, object>
                {
                    ["min"] = index == 0 ? min : min + (max - min) * (index * 0.25),
                    ["max"] = index == 3 ? max : min + (max - min) * ((index + 1) * 0.25),
                    ["_meta"] = new Dictionary<string, object?>(meta)
                };
        }
        return result;
    }

    public static string MapLegacyZone(string? classification)
    {
        var name = (classification ?? "").ToUpperInvariant();
        if (name.Contains("BAIXO") || name.Contains("LOW")) return "low";
        if (name.Contains("MODERADO") || name.Contains("MODERATED")) return "moderated";
        if (name.Contains("ELEVADO") || name.Contains("ELEVATED")) return "elevated";
        if (name.Contains("ALTO") || name.Contains("HIGH")) return "high";
        return "default";
    }
}

// This mapper deliberately retains the modern Normal/Leve/Moderado/Grave contract.
internal static class ClienteNormalidadesZonasBuilder
{
    public static Dictionary<string, Dictionary<string, Dictionary<string, object>>> Build(IEnumerable<dynamic> rows)
    {
        var resultado = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
        foreach (var row in rows)
        {
            var variavel = (string)row.VARIAVEL;
            var sexo = (string?)row.SEXO ?? "U";
            if (!resultado.ContainsKey(variavel)) resultado[variavel] = new();
            if (!resultado[variavel].ContainsKey(sexo)) resultado[variavel][sexo] = new();

            var zona = NormalidadeZonas.MapZona((string?)row.CLASSIFICACAO);
            var meta = new Dictionary<string, object?>();
            if (row.PAGINA_REFERENCIA != null) meta["Pagina"] = Convert.ToDouble(row.PAGINA_REFERENCIA);
            if (row.REFERENCIA_TITULO != null) meta["Fonte"] = (string)row.REFERENCIA_TITULO;
            if (row.REFERENCIA_ANO != null) meta["Ano"] = row.REFERENCIA_ANO;

            resultado[variavel][sexo][zona] = new Dictionary<string, object>
            {
                ["min"] = row.VALORMIN is null ? null! : Convert.ToDouble(row.VALORMIN),
                ["max"] = row.VALORMAX is null ? null! : Convert.ToDouble(row.VALORMAX),
                ["_meta"] = meta
            };
        }

        foreach (var (varName, sexos) in resultado.ToList())
        {
            foreach (var (sexo, zonas) in sexos.ToList())
            {
                if (zonas.ContainsKey("default") && zonas.Count == 1 && zonas["default"] is Dictionary<string, object> def)
                {
                    if (def["min"] is null || def["max"] is null) continue;
                    var min = Convert.ToDouble(def["min"]);
                    var max = Convert.ToDouble(def["max"]);
                    foreach (var (zn, data) in CalcZonas(min, max, def.GetValueOrDefault("_meta") as Dictionary<string, object?>))
                        if (zn != "default") resultado[varName][sexo][zn] = data;
                }
            }
        }
        return resultado;
    }

    private static Dictionary<string, Dictionary<string, object>> CalcZonas(double vmin, double vmax, Dictionary<string, object?>? meta)
    {
        var range = vmax - vmin;
        var q1 = vmin + range * 0.25;
        var q2 = vmin + range * 0.50;
        var q3 = vmin + range * 0.75;
        var m = meta ?? new Dictionary<string, object?>();
        Dictionary<string, object> Z(double a, double b) => new() { ["min"] = a, ["max"] = b, ["_meta"] = m };
        return new()
        {
            ["low"] = Z(vmin, q1),
            ["moderated"] = Z(q1, q2),
            ["elevated"] = Z(q2, q3),
            ["high"] = Z(q3, vmax)
        };
    }
}

public static class ClienteNormalidadesBuilder
{
    public static Dictionary<string, Dictionary<string, Dictionary<string, object>>> Build(
        IEnumerable<dynamic> rows,
        IReadOnlyDictionary<string, Dictionary<string, string>>? comentariosPorVariavelSexo = null)
    {
        var resultado = ClienteNormalidadesZonasBuilder.Build(rows);

        if (comentariosPorVariavelSexo is null || comentariosPorVariavelSexo.Count == 0)
            return resultado;

        foreach (var (variavel, porSexo) in comentariosPorVariavelSexo)
        {
            if (porSexo.Count == 0) continue;
            if (!resultado.ContainsKey(variavel))
                resultado[variavel] = new Dictionary<string, Dictionary<string, object>>();

            string? display = null;
            if (porSexo.TryGetValue("A", out var a) && !string.IsNullOrWhiteSpace(a))
                display = a.Trim();
            else
            {
                var parts = new List<string>();
                if (porSexo.TryGetValue("F", out var f) && !string.IsNullOrWhiteSpace(f)) parts.Add($"F: {f.Trim()}");
                if (porSexo.TryGetValue("M", out var m) && !string.IsNullOrWhiteSpace(m)) parts.Add($"M: {m.Trim()}");
                display = parts.Count > 0 ? string.Join("; ", parts) : null;
            }

            var payload = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(display))
                payload["texto"] = display;
            foreach (var (sexo, texto) in porSexo)
            {
                if (!string.IsNullOrWhiteSpace(texto))
                    payload[sexo] = texto.Trim();
            }

            resultado[variavel]["_comentario_texto"] = payload;
        }

        return resultado;
    }
}
