using System.Data;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Models;

namespace MdwConteudos.Api.Modules.Web;

public class ScriptsService
{
    private readonly string _connectionString;
    private readonly string _migrationRoot;
    private readonly string _staticUploadsRoot;
    private readonly string? _legacyRoot;
    private readonly IConfiguration _config;
    private readonly ILogger<ScriptsService> _logger;

    public ScriptsService(
        IConfiguration config,
        ILogger<ScriptsService> logger,
        IWebHostEnvironment env)
    {
        _config = config;
        _connectionString = EnvFileLoader.GetFirebirdConnectionString(config);
        _logger = logger;
        _migrationRoot = MigrationRootResolver.ResolveMigrationRoot(config, env.ContentRootPath);
        _staticUploadsRoot = MigrationRootResolver.StaticUploadsRoot(_migrationRoot);
        _legacyRoot = MigrationRootResolver.ResolveLegacyRoot(config, _migrationRoot, env.ContentRootPath);
    }

    private IDbConnection CreateConnection() => new FbConnection(_connectionString);

    private static int SafeInt(object? value, int fallback = 0)
    {
        if (value is null || value is DBNull) return fallback;
        if (value is bool b) return b ? 1 : 0;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed;
        return fallback;
    }

    public async Task<IReadOnlyList<PacoteDto>> GetPacotesAsync()
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<PacoteDto>(
            "SELECT CODPACOTE AS CodPacote, NOME AS Nome, DESCRICAO AS Descricao FROM PACOTES ORDER BY NOME");
        return rows.ToList();
    }

    public async Task<PagedScriptsResponse> ListScriptsAsync(
        string? nome, string? sistema, int? pacote, int? aprovado, int? ativo, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var offset = (page - 1) * pageSize;

        var where = new List<string>();
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(nome))
        {
            where.Add("UPPER(s.NOME) LIKE UPPER(@nome)");
            parameters.Add("nome", $"%{nome.Trim()}%");
        }
        if (sistema is "Laudos UX" or "Laudos Flex")
        {
            where.Add("s.SISTEMA = @sistema");
            parameters.Add("sistema", sistema);
        }
        if (pacote.HasValue)
        {
            where.Add("s.CODPACOTE = @pacote");
            parameters.Add("pacote", pacote.Value);
        }
        if (aprovado is 0 or 1)
        {
            where.Add("s.APROVADO = @aprovado");
            parameters.Add("aprovado", aprovado.Value);
        }
        if (ativo is 0 or 1)
        {
            where.Add("s.ATIVO = @ativo");
            parameters.Add("ativo", ativo.Value);
        }

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();

        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM SCRIPTLAUDO s {whereSql}", parameters);
        var totalPages = total > 0 ? (int)Math.Ceiling(total / (double)pageSize) : 0;

        parameters.Add("first", pageSize);
        parameters.Add("skip", offset);

        var sql = $@"
            SELECT FIRST @first SKIP @skip
                s.CODSCRIPTLAUDO, s.NOME, s.DESCRICAO, s.LINGUAGEM, s.CAMINHO_PROJETO, s.SISTEMA,
                s.APROVADO, s.DATA_VERIFICACAO, s.ATIVO,
                CASE WHEN s.ARQUIVO_JSON IS NOT NULL THEN 1 ELSE 0 END AS TEM_JSON,
                s.APROVADO_POR, p.NOME AS NOME_PACOTE, s.CAMINHO_AZURE,
                CASE WHEN s.DLL IS NOT NULL THEN 1 ELSE 0 END AS TEM_DLL,
                CASE WHEN EXISTS (SELECT 1 FROM SCRIPTLAUDOMRD m WHERE m.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO) THEN 1 ELSE 0 END AS TEM_MRD,
                s.CRIADO_POR, s.LINK_TESTE
            FROM SCRIPTLAUDO s
            LEFT JOIN PACOTES p ON s.CODPACOTE = p.CODPACOTE
            {whereSql}
            ORDER BY s.NOME";

        var rows = (await conn.QueryAsync(sql, parameters)).ToList();
        var items = new List<ScriptListItemDto>();

        foreach (var row in rows)
        {
            var d = (IDictionary<string, object>)row;
            int cod = Convert.ToInt32(d["CODSCRIPTLAUDO"]);
            var variaveis = (await conn.QueryAsync<ScriptVariableDto>(@"
                SELECT v.VARIAVEL AS Variavel, v.NOME AS Nome
                FROM SCRIPTLAUDO_VARIAVEL sv
                JOIN VARIAVEIS v ON sv.CODVARIAVEL = v.CODVARIAVEL
                WHERE sv.CODSCRIPTLAUDO = @cod ORDER BY v.NOME", new { cod })).ToList();

            var arquivos = (await conn.QueryAsync<ScriptFileDto>(@"
                SELECT TIPO AS Tipo, CAMINHO AS Caminho, NOME_ARQUIVO AS NomeArquivo
                FROM SCRIPTARQUIVOS WHERE CODSCRIPTLAUDO = @cod ORDER BY NOME_ARQUIVO", new { cod })).ToList();

            int? codVersao = null;
            string? ultimaVersao = null;
            var versaoRow = await conn.QueryFirstOrDefaultAsync(@"
                SELECT CODVERSAO, NUMERO_VERSAO FROM SCRIPTVERSOES
                WHERE CODSCRIPTLAUDO = @cod AND ATIVO = 'T'
                ORDER BY DATA_CRIACAO DESC ROWS 1", new { cod });
            if (versaoRow != null)
            {
                var vd = (IDictionary<string, object>)versaoRow;
                codVersao = Convert.ToInt32(vd["CODVERSAO"]);
                ultimaVersao = vd["NUMERO_VERSAO"]?.ToString();
            }

            var imagens = arquivos.Where(a => a.Tipo == "IMAGEM").ToList();
            var pdfs = arquivos.Where(a => a.Tipo == "PDF").ToList();

            if (codVersao.HasValue)
            {
                var arqVersao = (await conn.QueryAsync<ScriptFileDto>(@"
                    SELECT TIPO AS Tipo, CAMINHO AS Caminho, NOME_ARQUIVO AS NomeArquivo
                    FROM SCRIPTVERSAOARQUIVOS WHERE CODVERSAO = @cod ORDER BY DATA_UPLOAD DESC",
                    new { cod = codVersao.Value })).ToList();
                var iv = arqVersao.Where(a => a.Tipo == "IMAGEM").ToList();
                var pv = arqVersao.Where(a => a.Tipo == "PDF").ToList();
                if (iv.Count > 0) imagens = iv;
                if (pv.Count > 0) pdfs = pv;
            }

            var mrdList = await ListMrdAsync(conn, cod);

            items.Add(new ScriptListItemDto(
                cod,
                d["NOME"]?.ToString() ?? "",
                d["DESCRICAO"]?.ToString(),
                d["LINGUAGEM"]?.ToString(),
                d["CAMINHO_PROJETO"]?.ToString(),
                d["SISTEMA"]?.ToString() ?? "",
                SafeInt(d.TryGetValue("APROVADO", out var aprovadoRaw) ? aprovadoRaw : null),
                d["DATA_VERIFICACAO"] as DateTime?,
                SafeInt(d.TryGetValue("ATIVO", out var ativoRaw) ? ativoRaw : null),
                SafeInt(d.TryGetValue("TEM_JSON", out var temJsonRaw) ? temJsonRaw : null) == 1,
                d["APROVADO_POR"]?.ToString(),
                d["NOME_PACOTE"]?.ToString(),
                d["CAMINHO_AZURE"]?.ToString(),
                SafeInt(d.TryGetValue("TEM_DLL", out var temDllRaw) ? temDllRaw : null) == 1,
                SafeInt(d.TryGetValue("TEM_MRD", out var temMrdRaw) ? temMrdRaw : null) == 1,
                d["CRIADO_POR"]?.ToString(),
                d["LINK_TESTE"]?.ToString(),
                ultimaVersao,
                codVersao,
                variaveis,
                imagens,
                pdfs,
                mrdList
            ));
        }

        return new PagedScriptsResponse(items, page, totalPages, total);
    }

    private static async Task<IReadOnlyList<ScriptMrdDto>> ListMrdAsync(FbConnection conn, int cod)
    {
        var rows = await conn.QueryAsync(@"
            SELECT CODSCRIPTMRD, NOME_ARQUIVO, PADRAO, ORDEM FROM SCRIPTLAUDOMRD
            WHERE CODSCRIPTLAUDO = @cod
            ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, ORDEM, CODSCRIPTMRD", new { cod });
        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            return new ScriptMrdDto(
                Convert.ToInt32(d["CODSCRIPTMRD"]),
                d["NOME_ARQUIVO"]?.ToString() ?? "",
                (d["PADRAO"]?.ToString() ?? "F").Trim().ToUpperInvariant() == "T",
                d["ORDEM"] as int?
            );
        }).ToList();
    }

    public async Task<ScriptDetailDto?> GetScriptAsync(int id)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODSCRIPTLAUDO, NOME, DESCRICAO, LINGUAGEM, CAMINHO_PROJETO, CAMINHO_AZURE, LINK_TESTE,
                   SISTEMA, CODPACOTE, APROVADO, APROVADO_POR, ATIVO, CRIADO_POR,
                   CASE WHEN ARQUIVO_JSON IS NOT NULL THEN 1 ELSE 0 END AS TEM_JSON,
                   CASE WHEN DLL IS NOT NULL THEN 1 ELSE 0 END AS TEM_DLL
            FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @id", new { id });
        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        var vars = (await conn.QueryAsync<int>(
            "SELECT CODVARIAVEL FROM SCRIPTLAUDO_VARIAVEL WHERE CODSCRIPTLAUDO = @id", new { id })).ToList();
        var arquivos = (await conn.QueryAsync<ScriptFileDto>(@"
            SELECT TIPO AS Tipo, CAMINHO AS Caminho, NOME_ARQUIVO AS NomeArquivo
            FROM SCRIPTARQUIVOS WHERE CODSCRIPTLAUDO = @id", new { id })).ToList();
        var mrd = await ListMrdAsync(conn, id);
        return new ScriptDetailDto(
            id,
            d["NOME"]!.ToString()!,
            d["DESCRICAO"]?.ToString(),
            d["LINGUAGEM"]?.ToString(),
            d["CAMINHO_PROJETO"]?.ToString(),
            d["CAMINHO_AZURE"]?.ToString(),
            d["LINK_TESTE"]?.ToString(),
            d["SISTEMA"]!.ToString()!,
            d["CODPACOTE"] as int?,
            SafeInt(d.TryGetValue("APROVADO", out var aprovadoRaw) ? aprovadoRaw : null),
            d["APROVADO_POR"]?.ToString(),
            SafeInt(d.TryGetValue("ATIVO", out var ativoRaw) ? ativoRaw : null),
            d["CRIADO_POR"]?.ToString(),
            SafeInt(d.TryGetValue("TEM_JSON", out var temJsonRaw) ? temJsonRaw : null) == 1,
            SafeInt(d.TryGetValue("TEM_DLL", out var temDllRaw) ? temDllRaw : null) == 1,
            mrd,
            arquivos.Where(a => a.Tipo == "IMAGEM").ToList(),
            arquivos.Where(a => a.Tipo == "PDF").ToList(),
            vars
        );
    }

    public async Task<bool> VerificarNomeAsync(string nome, int? excludeId = null)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var sql = excludeId.HasValue
            ? "SELECT 1 FROM SCRIPTLAUDO WHERE UPPER(NOME) = UPPER(@nome) AND CODSCRIPTLAUDO <> @id ROWS 1"
            : "SELECT 1 FROM SCRIPTLAUDO WHERE UPPER(NOME) = UPPER(@nome) ROWS 1";
        var found = await conn.ExecuteScalarAsync<int?>(sql, new { nome, id = excludeId });
        return found.HasValue;
    }

    public async Task<int> CreateScriptAsync(ScriptFormInput input)
    {
        ValidateForm(input, isNew: true);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var cod = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO SCRIPTLAUDO (NOME, CODPACOTE, DESCRICAO, LINGUAGEM, CAMINHO_PROJETO, CAMINHO_AZURE,
                LINK_TESTE, SISTEMA, APROVADO, APROVADO_POR, ATIVO, CRIADO_POR, ARQUIVO_JSON, DLL)
            VALUES (@Nome, @CodPacote, @Descricao, @Linguagem, @CaminhoProjeto, @CaminhoAzure, @LinkTeste,
                @Sistema, @Aprovado, @AprovadoPor, @Ativo, @CriadoPor, @Json, @Dll)
            RETURNING CODSCRIPTLAUDO",
            new
            {
                input.Nome,
                input.CodPacote,
                input.Descricao,
                input.Linguagem,
                input.CaminhoProjeto,
                input.CaminhoAzure,
                input.LinkTeste,
                input.Sistema,
                input.Aprovado,
                input.AprovadoPor,
                input.Ativo,
                input.CriadoPor,
                Json = input.ArquivoJson,
                Dll = input.ArquivoDll
            }, tx);

        await SaveAttachmentsAsync(conn, tx, cod, input);
        await SaveMrdFilesAsync(conn, tx, cod, input.Sistema, input.MrdFiles, isFirst: true);
        await tx.CommitAsync();
        return cod;
    }

    public async Task UpdateScriptAsync(int id, ScriptFormInput input)
    {
        ValidateForm(input, isNew: false);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(@"
            UPDATE SCRIPTLAUDO SET NOME=@Nome, CODPACOTE=@CodPacote, DESCRICAO=@Descricao, LINGUAGEM=@Linguagem,
                CAMINHO_PROJETO=@CaminhoProjeto, CAMINHO_AZURE=@CaminhoAzure, LINK_TESTE=@LinkTeste, SISTEMA=@Sistema,
                APROVADO=@Aprovado, APROVADO_POR=@AprovadoPor, ATIVO=@Ativo
            WHERE CODSCRIPTLAUDO=@id",
            new
            {
                id,
                input.Nome,
                input.CodPacote,
                input.Descricao,
                input.Linguagem,
                input.CaminhoProjeto,
                input.CaminhoAzure,
                input.LinkTeste,
                input.Sistema,
                input.Aprovado,
                input.AprovadoPor,
                input.Ativo
            }, tx);

        if (input.ArquivoJson != null)
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET ARQUIVO_JSON=@b WHERE CODSCRIPTLAUDO=@id",
                new { id, b = input.ArquivoJson }, tx);
        if (input.ArquivoDll != null)
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET DLL=@b WHERE CODSCRIPTLAUDO=@id",
                new { id, b = input.ArquivoDll }, tx);

        await SaveAttachmentsAsync(conn, tx, id, input);
        if (input.MrdFiles?.Count > 0)
            await SaveMrdFilesAsync(conn, tx, id, input.Sistema, input.MrdFiles, isFirst: false);

        await tx.CommitAsync();
    }

    private async Task SaveAttachmentsAsync(FbConnection conn, FbTransaction tx, int cod, ScriptFormInput input)
    {
        foreach (var img in input.Imagens ?? [])
        {
            var (path, name) = await SaveDiskFileAsync(img, "interfaces");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTARQUIVOS (CODSCRIPTLAUDO, TIPO, CAMINHO, NOME_ARQUIVO)
                VALUES (@cod, 'IMAGEM', @path, @name)", new { cod, path, name }, tx);
        }
        foreach (var pdf in input.Pdfs ?? [])
        {
            var (path, name) = await SaveDiskFileAsync(pdf, "impressoes");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTARQUIVOS (CODSCRIPTLAUDO, TIPO, CAMINHO, NOME_ARQUIVO)
                VALUES (@cod, 'PDF', @path, @name)", new { cod, path, name }, tx);
        }
    }

    private async Task SaveMrdFilesAsync(FbConnection conn, FbTransaction tx, int cod, string sistema,
        IList<UploadedFile>? files, bool isFirst)
    {
        if (files == null || files.Count == 0) return;
        var ordem = 0;
        foreach (var f in files)
        {
            var err = ValidateMrdExtension(sistema, f.FileName);
            if (err != null) throw new InvalidOperationException(err);
            var padrao = isFirst && ordem == 0 ? "T" : "F";
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTLAUDOMRD (CODSCRIPTLAUDO, NOME_ARQUIVO, ARQUIVO_MRD, PADRAO, ORDEM)
                VALUES (@cod, @nome, @blob, @padrao, @ordem)",
                new { cod, nome = f.FileName, blob = f.Content, padrao, ordem }, tx);
            ordem++;
        }
    }

    private async Task<(string path, string name)> SaveDiskFileAsync(UploadedFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName);
        var filename = $"{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(_staticUploadsRoot, folder);
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, filename);
        await File.WriteAllBytesAsync(full, file.Content);
        return ($"/static/uploads/{folder}/{filename}", file.FileName);
    }

    private static void ValidateForm(ScriptFormInput input, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(input.Nome)) throw new InvalidOperationException("Nome obrigatório.");
        if (string.IsNullOrWhiteSpace(input.CriadoPor) && isNew) throw new InvalidOperationException("Criado por obrigatório.");
        if (input.Sistema is not ("Laudos UX" or "Laudos Flex")) throw new InvalidOperationException("Sistema inválido.");
        if (isNew && input.Sistema == "Laudos UX" && input.ArquivoJson == null)
            throw new InvalidOperationException("JSON obrigatório para Laudos UX.");
    }

    public static string? ValidateMrdExtension(string sistema, string filename)
    {
        var fn = filename.ToLowerInvariant();
        if (sistema == "Laudos UX" && !fn.EndsWith(".json")) return "MRD para Laudos UX deve ser .json.";
        if (sistema == "Laudos Flex" && !fn.EndsWith(".mrd")) return "MRD para Laudos Flex deve ser .mrd.";
        return null;
    }

    public async Task ToggleAtivoAsync(int id)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE SCRIPTLAUDO SET ATIVO = CASE WHEN ATIVO = 1 THEN 0 ELSE 1 END
            WHERE CODSCRIPTLAUDO = @id", new { id });
    }

    public async Task ToggleAprovacaoAsync(int id, string? aprovadoPor)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE SCRIPTLAUDO SET APROVADO = CASE WHEN APROVADO = 1 THEN 0 ELSE 1 END,
                APROVADO_POR = CASE WHEN APROVADO = 0 THEN @ap ELSE APROVADO_POR END,
                DATA_VERIFICACAO = CURRENT_TIMESTAMP
            WHERE CODSCRIPTLAUDO = @id", new { id, ap = aprovadoPor });
    }

    public async Task<IReadOnlyList<object>> GetVariaveisForLinkAsync(int scriptId)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var all = await conn.QueryAsync(@"
            SELECT v.CODVARIAVEL, v.NOME, v.VARIAVEL AS SIGLA,
                   COALESCE(f.NOME, '') AS FORMULA,
                   COALESCE(CAST(n.DESCRICAO AS VARCHAR(500)), '') AS NORMALIDADE
            FROM VARIAVEIS v
            LEFT JOIN FORMULAS f ON v.CODFORMULA = f.CODFORMULA
            LEFT JOIN NORMALIDADES n ON v.CODNORMALIDADE = n.CODNORMALIDADE
            ORDER BY v.NOME");
        var linked = (await conn.QueryAsync<int>(
            "SELECT CODVARIAVEL FROM SCRIPTLAUDO_VARIAVEL WHERE CODSCRIPTLAUDO = @scriptId",
            new { scriptId })).ToHashSet();
        return all.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            var cod = Convert.ToInt32(d["CODVARIAVEL"]);
            return new
            {
                codVariavel = cod,
                nome = d["NOME"]?.ToString(),
                sigla = d["SIGLA"]?.ToString(),
                formula = d["FORMULA"]?.ToString(),
                normalidade = d["NORMALIDADE"]?.ToString(),
                associada = linked.Contains(cod)
            };
        }).ToList<object>();
    }

    public async Task SaveVariaveisAsync(int scriptId, IEnumerable<int> codVariaveis)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync("DELETE FROM SCRIPTLAUDO_VARIAVEL WHERE CODSCRIPTLAUDO = @scriptId",
            new { scriptId }, tx);
        foreach (var v in codVariaveis.Distinct())
        {
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTLAUDO_VARIAVEL (CODSCRIPTLAUDO, CODVARIAVEL) VALUES (@scriptId, @v)",
                new { scriptId, v }, tx);
        }
        await tx.CommitAsync();
    }

    public async Task AddMrdFilesOnlyAsync(int cod, string sistema, IList<UploadedFile> files)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var has = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SCRIPTLAUDOMRD WHERE CODSCRIPTLAUDO = @cod", new { cod }, tx);
        await SaveMrdFilesAsync(conn, tx, cod, sistema, files, isFirst: has == 0);
        await tx.CommitAsync();
    }

    public async Task DeleteMrdAsync(int codScriptMrd)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM SCRIPTLAUDOMRD WHERE CODSCRIPTMRD = @cod", new { cod = codScriptMrd });
    }

    public async Task<IReadOnlyList<VersaoDto>> ListVersoesAsync(
        int scriptId, string? numeroVersao = null, string? aprovado = null, string? ativo = null)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var where = new List<string> { "CODSCRIPTLAUDO = @scriptId" };
        var p = new DynamicParameters();
        p.Add("scriptId", scriptId);
        if (!string.IsNullOrWhiteSpace(numeroVersao))
        {
            where.Add("UPPER(NUMERO_VERSAO) LIKE UPPER(@num)");
            p.Add("num", $"%{numeroVersao.Trim()}%");
        }
        if (aprovado is "0" or "1")
        {
            where.Add(aprovado == "1" ? "APROVADO = 'T'" : "(APROVADO IS NULL OR APROVADO = 'F' OR APROVADO = 0)");
        }
        if (ativo is "0" or "1")
        {
            where.Add(ativo == "1" ? "ATIVO = 'T'" : "(ATIVO IS NULL OR ATIVO = 'F')");
        }
        var sql = $@"
            SELECT CODVERSAO, NUMERO_VERSAO, DATA_CRIACAO, ATIVO, APROVADO, OBSERVACOES,
                   USUARIO_RESPONSAVEL, DESCRICAO_ALTERACOES, APROVADO_POR
            FROM SCRIPTVERSOES WHERE {string.Join(" AND ", where)} ORDER BY DATA_CRIACAO DESC";
        var rows = await conn.QueryAsync(sql, p);
        return rows.Select(MapVersaoDto).ToList();
    }

    private static VersaoDto MapVersaoDto(dynamic r)
    {
        var d = (IDictionary<string, object>)r;
        return new VersaoDto(
            Convert.ToInt32(d["CODVERSAO"]),
            d["NUMERO_VERSAO"]?.ToString() ?? "",
            d["DATA_CRIACAO"] as DateTime?,
            d["ATIVO"]?.ToString(),
            NormalizeFlag(d.TryGetValue("APROVADO", out var ap) ? ap : null),
            d["OBSERVACOES"]?.ToString(),
            d["USUARIO_RESPONSAVEL"]?.ToString(),
            d["DESCRICAO_ALTERACOES"]?.ToString(),
            d["APROVADO_POR"]?.ToString()
        );
    }

    private static string? NormalizeFlag(object? value)
    {
        if (value is null or DBNull) return "F";
        var s = value.ToString()?.Trim().ToUpperInvariant();
        return s is "T" or "1" or "TRUE" or "S" or "Y" ? "T" : "F";
    }

    public async Task<VersaoCreateMetaDto?> GetVersaoCreateMetaAsync(int scriptId)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var script = await conn.QueryFirstOrDefaultAsync(@"
            SELECT s.NOME, s.DESCRICAO, s.SISTEMA, s.LINGUAGEM, p.NOME AS NOME_PACOTE
            FROM SCRIPTLAUDO s
            LEFT JOIN PACOTES p ON s.CODPACOTE = p.CODPACOTE
            WHERE s.CODSCRIPTLAUDO = @scriptId", new { scriptId });
        if (script == null) return null;
        var sd = (IDictionary<string, object>)script;
        var versoes = await ListVersoesAsync(scriptId);
        return new VersaoCreateMetaDto(
            sd["NOME"]!.ToString()!,
            sd["DESCRICAO"]?.ToString(),
            sd["SISTEMA"]!.ToString()!,
            sd["LINGUAGEM"]?.ToString(),
            sd["NOME_PACOTE"]?.ToString(),
            await GerarProximoNumeroVersaoAsync(conn, scriptId),
            versoes
        );
    }

    private static async Task<string> GerarProximoNumeroVersaoAsync(FbConnection conn, int scriptId)
    {
        var ultima = await conn.ExecuteScalarAsync<string?>(@"
            SELECT FIRST 1 NUMERO_VERSAO FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @scriptId ORDER BY DATA_CRIACAO DESC", new { scriptId });
        if (string.IsNullOrWhiteSpace(ultima)) return "V1.0";
        if (!ultima.StartsWith("V", StringComparison.OrdinalIgnoreCase)) return "V1.0";
        var numero = ultima[1..];
        var parts = numero.Split('.');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return "V1.0";
        return $"V{major}.{minor + 1}";
    }

    public async Task<ScriptVersionDetailDto?> GetVersaoAsync(int scriptId, int codVersao)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT sv.CODVERSAO, sv.CODSCRIPTLAUDO, sv.NUMERO_VERSAO,
                   sv.DESCRICAO_ALTERACOES, sv.ALTERACOES_INTERFACE, sv.ALTERACOES_CODIGO,
                   sv.DATA_CRIACAO, sv.USUARIO_RESPONSAVEL, sv.ATIVO, sv.APROVADO,
                   sv.APROVADO_POR, sv.DATA_APROVACAO, sv.OBSERVACOES,
                   CASE WHEN sv.ARQUIVO_JSON IS NOT NULL THEN 1 ELSE 0 END AS TEM_JSON,
                   CASE WHEN sv.ARQUIVO_DLL IS NOT NULL THEN 1 ELSE 0 END AS TEM_DLL,
                   s.NOME AS SCRIPT_NOME, s.DESCRICAO AS SCRIPT_DESCRICAO, s.SISTEMA,
                   s.LINGUAGEM, p.NOME AS NOME_PACOTE
            FROM SCRIPTVERSOES sv
            JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
            LEFT JOIN PACOTES p ON s.CODPACOTE = p.CODPACOTE
            WHERE sv.CODSCRIPTLAUDO = @scriptId AND sv.CODVERSAO = @codVersao",
            new { scriptId, codVersao });

        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        var arquivos = await ListVersaoArquivosAsync(conn, codVersao);
        var mrd = await ListVersaoMrdAsync(conn, codVersao);
        var historico = await ListVersaoHistoricoAsync(conn, codVersao);

        return new ScriptVersionDetailDto(
            Convert.ToInt32(d["CODVERSAO"]),
            Convert.ToInt32(d["CODSCRIPTLAUDO"]),
            d["NUMERO_VERSAO"]?.ToString() ?? "",
            d["SCRIPT_NOME"]?.ToString() ?? "",
            d["SCRIPT_DESCRICAO"]?.ToString(),
            d["SISTEMA"]?.ToString() ?? "",
            d["LINGUAGEM"]?.ToString(),
            d["NOME_PACOTE"]?.ToString(),
            d["DESCRICAO_ALTERACOES"]?.ToString(),
            d["ALTERACOES_INTERFACE"]?.ToString(),
            d["ALTERACOES_CODIGO"]?.ToString(),
            d["DATA_CRIACAO"] as DateTime?,
            d["USUARIO_RESPONSAVEL"]?.ToString(),
            d["ATIVO"]?.ToString(),
            NormalizeFlag(d.TryGetValue("APROVADO", out var apv) ? apv : null),
            d["APROVADO_POR"]?.ToString(),
            d["DATA_APROVACAO"] as DateTime?,
            d["OBSERVACOES"]?.ToString(),
            Convert.ToInt32(d["TEM_JSON"]) == 1,
            Convert.ToInt32(d["TEM_DLL"]) == 1,
            arquivos.Where(a => a.Tipo == "IMAGEM").ToList(),
            arquivos.Where(a => a.Tipo == "PDF").ToList(),
            mrd,
            historico
        );
    }

    private async Task<IReadOnlyList<ScriptVersionFileDto>> ListVersaoArquivosAsync(FbConnection conn, int codVersao)
    {
        try
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODARQUIVO, TIPO, CAMINHO, NOME_ARQUIVO, DATA_UPLOAD, USUARIO_UPLOAD
                FROM SCRIPTVERSAOARQUIVOS
                WHERE CODVERSAO = @codVersao
                ORDER BY DATA_UPLOAD DESC", new { codVersao });
            return rows.Select(r =>
            {
                var d = (IDictionary<string, object>)r;
                return new ScriptVersionFileDto(
                    Convert.ToInt32(d["CODARQUIVO"]),
                    d["TIPO"]?.ToString() ?? "",
                    d["CAMINHO"]?.ToString(),
                    d["NOME_ARQUIVO"]?.ToString() ?? "",
                    d["DATA_UPLOAD"] as DateTime?,
                    d["USUARIO_UPLOAD"]?.ToString()
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar arquivos da versao {CodVersao}", codVersao);
            return [];
        }
    }

    private async Task<IReadOnlyList<ScriptVersionMrdDto>> ListVersaoMrdAsync(FbConnection conn, int codVersao)
    {
        var rows = await conn.QueryAsync(@"
            SELECT CODVERSAOMRD, NOME_ARQUIVO, PADRAO, ORDEM
            FROM SCRIPTVERSAOMRD
            WHERE CODVERSAO = @codVersao
            ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, ORDEM, CODVERSAOMRD", new { codVersao });
        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            return new ScriptVersionMrdDto(
                Convert.ToInt32(d["CODVERSAOMRD"]),
                d["NOME_ARQUIVO"]?.ToString() ?? "",
                (d["PADRAO"]?.ToString() ?? "F").Trim().ToUpperInvariant() == "T",
                d["ORDEM"] as int?
            );
        }).ToList();
    }

    private async Task<IReadOnlyList<ScriptVersionHistoryDto>> ListVersaoHistoricoAsync(FbConnection conn, int codVersao)
    {
        try
        {
            var rows = await conn.QueryAsync(@"
                SELECT TIPO_ALTERACAO, DESCRICAO, USUARIO, DATA_ALTERACAO
                FROM SCRIPTVERSAOHISTORICO
                WHERE CODVERSAO_DESTINO = @codVersao
                ORDER BY DATA_ALTERACAO DESC", new { codVersao });
            return rows.Select(r =>
            {
                var d = (IDictionary<string, object>)r;
                return new ScriptVersionHistoryDto(
                    d["TIPO_ALTERACAO"]?.ToString() ?? "",
                    d["DESCRICAO"]?.ToString(),
                    d["USUARIO"]?.ToString(),
                    d["DATA_ALTERACAO"] as DateTime?
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar historico da versao {CodVersao}", codVersao);
            return [];
        }
    }

    public async Task<int> CreateVersaoAsync(int scriptId, VersaoFormInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NumeroVersao))
            throw new InvalidOperationException("Número da versão é obrigatório.");
        if (string.IsNullOrWhiteSpace(input.DescricaoAlteracoes))
            throw new InvalidOperationException("Descrição das alterações é obrigatória.");

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var sistema = await conn.ExecuteScalarAsync<string>(
            "SELECT SISTEMA FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId })
            ?? throw new InvalidOperationException("Script não encontrado.");

        var exists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @scriptId AND NUMERO_VERSAO = @num",
            new { scriptId, num = input.NumeroVersao.Trim() });
        if (exists > 0) throw new InvalidOperationException("Já existe uma versão com este número.");

        byte[]? arquivoJson = input.ArquivoJson;
        byte[]? arquivoDll = input.ArquivoDll;
        if (arquivoJson == null && arquivoDll == null)
        {
            var blobs = await conn.QueryFirstOrDefaultAsync(@"
                SELECT ARQUIVO_JSON, DLL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId });
            if (blobs != null)
            {
                var bd = (IDictionary<string, object>)blobs;
                arquivoJson = BlobToBytes(bd.TryGetValue("ARQUIVO_JSON", out var j) ? j : null);
                arquivoDll = BlobToBytes(bd.TryGetValue("DLL", out var d) ? d : null);
            }
        }

        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(
            "UPDATE SCRIPTVERSOES SET ATIVO = 'F' WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId }, tx);

        var codVersao = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO SCRIPTVERSOES (
                CODSCRIPTLAUDO, NUMERO_VERSAO, DESCRICAO_ALTERACOES,
                ALTERACOES_INTERFACE, ALTERACOES_CODIGO, USUARIO_RESPONSAVEL,
                OBSERVACOES, ATIVO, DATA_CRIACAO
            ) VALUES (
                @scriptId, @num, @desc, @iface, @cod, @user, @obs, 'T', CURRENT_TIMESTAMP
            ) RETURNING CODVERSAO",
            new
            {
                scriptId,
                num = input.NumeroVersao.Trim(),
                desc = input.DescricaoAlteracoes,
                iface = input.AlteracoesInterface,
                cod = input.AlteracoesCodigo,
                user = input.UsuarioResponsavel,
                obs = input.Observacoes
            }, tx);

        if (arquivoJson != null || arquivoDll != null)
        {
            var sets = new List<string>();
            var p = new DynamicParameters();
            p.Add("codVersao", codVersao);
            if (arquivoJson != null) { sets.Add("ARQUIVO_JSON = @json"); p.Add("json", arquivoJson); }
            if (arquivoDll != null) { sets.Add("ARQUIVO_DLL = @dll"); p.Add("dll", arquivoDll); }
            await conn.ExecuteAsync($"UPDATE SCRIPTVERSOES SET {string.Join(", ", sets)} WHERE CODVERSAO = @codVersao", p, tx);
        }

        var mrdFiles = input.MrdFiles?.Where(f => !string.IsNullOrWhiteSpace(f.FileName)).ToList();
        if (mrdFiles is { Count: > 0 })
        {
            for (var i = 0; i < mrdFiles.Count; i++)
            {
                var f = mrdFiles[i];
                var err = ValidateMrdExtension(sistema, f.FileName);
                if (err != null) throw new InvalidOperationException(err);
                var isPadrao = input.MrdPadraoIdx.HasValue ? input.MrdPadraoIdx == i : i == 0;
                await InsertVersaoMrdAsync(conn, tx, codVersao, f.FileName, f.Content, isPadrao);
            }
        }
        else
        {
            await CopyScriptMrdToVersaoAsync(conn, tx, scriptId, codVersao);
        }

        foreach (var img in input.Imagens ?? [])
        {
            var (path, name) = await SaveVersaoDiskFileAsync(img, "versoes/interfaces", $"versao_{codVersao}_interface");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTVERSAOARQUIVOS (CODVERSAO, TIPO, CAMINHO, NOME_ARQUIVO, USUARIO_UPLOAD)
                VALUES (@codVersao, 'IMAGEM', @path, @name, @user)",
                new { codVersao, path, name, user = input.UsuarioResponsavel }, tx);
        }
        foreach (var pdf in input.Pdfs ?? [])
        {
            var (path, name) = await SaveVersaoDiskFileAsync(pdf, "versoes/impressoes", $"versao_{codVersao}_impressao");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTVERSAOARQUIVOS (CODVERSAO, TIPO, CAMINHO, NOME_ARQUIVO, USUARIO_UPLOAD)
                VALUES (@codVersao, 'PDF', @path, @name, @user)",
                new { codVersao, path, name, user = input.UsuarioResponsavel }, tx);
        }

        await InsertVersaoHistoricoAsync(conn, tx, codVersao, "CRIACAO",
            $"Versão {input.NumeroVersao.Trim()} criada", input.UsuarioResponsavel);
        await tx.CommitAsync();
        return codVersao;
    }

    public async Task UpdateVersaoAsync(int codVersao, VersaoFormInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DescricaoAlteracoes))
            throw new InvalidOperationException("Descrição das alterações é obrigatória.");

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var meta = await conn.QueryFirstOrDefaultAsync(@"
            SELECT sv.CODSCRIPTLAUDO, sv.NUMERO_VERSAO, s.SISTEMA
            FROM SCRIPTVERSOES sv
            JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
            WHERE sv.CODVERSAO = @codVersao", new { codVersao });
        if (meta == null) throw new InvalidOperationException("Versão não encontrada.");
        var md = (IDictionary<string, object>)meta;
        var scriptId = Convert.ToInt32(md["CODSCRIPTLAUDO"]);
        var numeroVersao = md["NUMERO_VERSAO"]?.ToString() ?? "";
        var sistema = md["SISTEMA"]?.ToString() ?? "";

        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(@"
            UPDATE SCRIPTVERSOES SET
                DESCRICAO_ALTERACOES = @desc,
                ALTERACOES_INTERFACE = @iface,
                ALTERACOES_CODIGO = @cod,
                OBSERVACOES = @obs
            WHERE CODVERSAO = @codVersao",
            new
            {
                codVersao,
                desc = input.DescricaoAlteracoes,
                iface = input.AlteracoesInterface,
                cod = input.AlteracoesCodigo,
                obs = input.Observacoes
            }, tx);

        if (input.ArquivoJson != null)
            await conn.ExecuteAsync("UPDATE SCRIPTVERSOES SET ARQUIVO_JSON = @b WHERE CODVERSAO = @codVersao",
                new { b = input.ArquivoJson, codVersao }, tx);
        if (input.ArquivoDll != null)
            await conn.ExecuteAsync("UPDATE SCRIPTVERSOES SET ARQUIVO_DLL = @b WHERE CODVERSAO = @codVersao",
                new { b = input.ArquivoDll, codVersao }, tx);

        foreach (var codMrd in input.MrdExcluir ?? [])
        {
            await conn.ExecuteAsync(@"
                DELETE FROM SCRIPTVERSAOMRD WHERE CODVERSAOMRD = @id AND CODVERSAO = @codVersao",
                new { id = codMrd, codVersao }, tx);
        }

        var hasMrd = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SCRIPTVERSAOMRD WHERE CODVERSAO = @codVersao", new { codVersao }, tx) > 0;
        foreach (var f in input.MrdFiles ?? [])
        {
            if (string.IsNullOrWhiteSpace(f.FileName)) continue;
            var err = ValidateMrdExtension(sistema, f.FileName);
            if (err != null) throw new InvalidOperationException(err);
            await InsertVersaoMrdAsync(conn, tx, codVersao, f.FileName, f.Content, padrao: !hasMrd);
            hasMrd = true;
        }
        if (input.MrdPadraoCod.HasValue)
            await SetVersaoMrdPadraoAsync(conn, tx, codVersao, input.MrdPadraoCod.Value);
        else
        {
            var remaining = await ListVersaoMrdAsync(conn, codVersao);
            if (remaining.Count > 0 && remaining.All(m => !m.Padrao))
                await SetVersaoMrdPadraoAsync(conn, tx, codVersao, remaining[0].CodVersaoMrd);
        }

        foreach (var img in input.Imagens ?? [])
        {
            var (path, name) = await SaveVersaoDiskFileAsync(img, "versoes/interfaces", $"versao_{codVersao}_interface");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTVERSAOARQUIVOS (CODVERSAO, TIPO, CAMINHO, NOME_ARQUIVO, USUARIO_UPLOAD)
                VALUES (@codVersao, 'IMAGEM', @path, @name, @user)",
                new { codVersao, path, name, user = input.UsuarioResponsavel }, tx);
        }
        foreach (var pdf in input.Pdfs ?? [])
        {
            var (path, name) = await SaveVersaoDiskFileAsync(pdf, "versoes/impressoes", $"versao_{codVersao}_impressao");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTVERSAOARQUIVOS (CODVERSAO, TIPO, CAMINHO, NOME_ARQUIVO, USUARIO_UPLOAD)
                VALUES (@codVersao, 'PDF', @path, @name, @user)",
                new { codVersao, path, name, user = input.UsuarioResponsavel }, tx);
        }

        await InsertVersaoHistoricoAsync(conn, tx, codVersao, "EDICAO",
            $"Versão {numeroVersao} editada", input.UsuarioResponsavel);
        await tx.CommitAsync();
        _ = scriptId;
    }

    public async Task ActivateVersaoAsync(int codVersao, string usuario)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODSCRIPTLAUDO, NUMERO_VERSAO FROM SCRIPTVERSOES WHERE CODVERSAO = @codVersao",
            new { codVersao });
        if (row == null) throw new InvalidOperationException("Versão não encontrada.");
        var d = (IDictionary<string, object>)row;
        var scriptId = Convert.ToInt32(d["CODSCRIPTLAUDO"]);
        var numero = d["NUMERO_VERSAO"]?.ToString() ?? "";
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(
            "UPDATE SCRIPTVERSOES SET ATIVO = 'F' WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId }, tx);
        await conn.ExecuteAsync(
            "UPDATE SCRIPTVERSOES SET ATIVO = 'T' WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await InsertVersaoHistoricoAsync(conn, tx, codVersao, "ATIVACAO", $"Versão {numero} ativada", usuario);
        await tx.CommitAsync();
    }

    public async Task ApproveVersaoAsync(int codVersao, string usuario)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT NUMERO_VERSAO, APROVADO FROM SCRIPTVERSOES WHERE CODVERSAO = @codVersao", new { codVersao });
        if (row == null) throw new InvalidOperationException("Versão não encontrada.");
        var d = (IDictionary<string, object>)row;
        if (NormalizeFlag(d["APROVADO"]) == "T")
            throw new InvalidOperationException("Esta versão já foi aprovada.");
        var numero = d["NUMERO_VERSAO"]?.ToString() ?? "";
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(@"
            UPDATE SCRIPTVERSOES SET APROVADO = 'T', APROVADO_POR = @user, DATA_APROVACAO = CURRENT_TIMESTAMP
            WHERE CODVERSAO = @codVersao", new { user = usuario, codVersao }, tx);
        await InsertVersaoHistoricoAsync(conn, tx, codVersao, "APROVACAO",
            $"Versão {numero} aprovada por {usuario}", usuario);
        await tx.CommitAsync();
    }

    public async Task DeleteVersaoAsync(int codVersao, string usuario)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODSCRIPTLAUDO, NUMERO_VERSAO, ATIVO FROM SCRIPTVERSOES WHERE CODVERSAO = @codVersao",
            new { codVersao });
        if (row == null) throw new InvalidOperationException("Versão não encontrada.");
        var d = (IDictionary<string, object>)row;
        var scriptId = Convert.ToInt32(d["CODSCRIPTLAUDO"]);
        var numero = d["NUMERO_VERSAO"]?.ToString() ?? "";
        if ((d["ATIVO"]?.ToString() ?? "").Trim().ToUpperInvariant() == "T")
            throw new InvalidOperationException("Não é possível excluir a versão ativa. Ative outra versão antes.");
        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SCRIPTVERSOES WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId });
        if (total <= 1)
            throw new InvalidOperationException("Não é possível excluir a única versão existente do script.");

        var caminhos = (await conn.QueryAsync<string>(
            "SELECT CAMINHO FROM SCRIPTVERSAOARQUIVOS WHERE CODVERSAO = @codVersao", new { codVersao }))
            .Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await InsertVersaoHistoricoAsync(conn, tx, codVersao, "EXCLUSAO",
                $"Versão {numero} excluída por {usuario}", usuario);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Histórico de exclusão não registrado para versão {CodVersao}", codVersao);
        }
        await conn.ExecuteAsync("DELETE FROM SCRIPTVERSAOARQUIVOS WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await conn.ExecuteAsync("DELETE FROM SCRIPTVERSAOMRD WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await conn.ExecuteAsync("DELETE FROM SCRIPTVERSOES WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await tx.CommitAsync();

        foreach (var caminho in caminhos)
        {
            try
            {
                var path = ResolveLegacyPath(caminho);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao excluir arquivo físico da versão {CodVersao}", codVersao);
            }
        }
    }

    public async Task<(byte[] content, string filename, string mime)?> ExportVersaoArquivoAsync(int codVersao, string tipo)
    {
        tipo = tipo.ToLowerInvariant();
        if (tipo == "mrd") return await ExportVersaoMrdAsync(codVersao, null);
        if (tipo is not ("json" or "dll")) return null;

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var campo = tipo == "json" ? "ARQUIVO_JSON" : "ARQUIVO_DLL";
        var row = await conn.QueryFirstOrDefaultAsync($@"
            SELECT sv.{campo}, sv.NUMERO_VERSAO, s.NOME
            FROM SCRIPTVERSOES sv
            JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
            WHERE sv.CODVERSAO = @codVersao", new { codVersao });
        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        var bytes = BlobToBytes(d[campo]);
        if (bytes == null || bytes.Length == 0) return null;
        var nome = d["NOME"]?.ToString() ?? "script";
        var versao = d["NUMERO_VERSAO"]?.ToString() ?? codVersao.ToString();
        var ext = tipo == "json" ? ".json" : ".dll";
        var mime = tipo == "json" ? "application/json" : "application/octet-stream";
        return (bytes, $"{nome}_{versao}{ext}", mime);
    }

    public async Task<(byte[] content, string filename, string mime)?> ExportVersaoMrdAsync(int codVersao, int? codVersaoMrd)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        dynamic? row;
        if (codVersaoMrd.HasValue)
        {
            row = await conn.QueryFirstOrDefaultAsync(@"
                SELECT vm.NOME_ARQUIVO, vm.ARQUIVO_MRD, sv.NUMERO_VERSAO, s.NOME, s.SISTEMA, s.LINGUAGEM
                FROM SCRIPTVERSAOMRD vm
                JOIN SCRIPTVERSOES sv ON vm.CODVERSAO = sv.CODVERSAO
                JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
                WHERE vm.CODVERSAOMRD = @id AND vm.CODVERSAO = @codVersao",
                new { id = codVersaoMrd, codVersao });
        }
        else
        {
            row = await conn.QueryFirstOrDefaultAsync(@"
                SELECT FIRST 1 vm.NOME_ARQUIVO, vm.ARQUIVO_MRD, sv.NUMERO_VERSAO, s.NOME, s.SISTEMA, s.LINGUAGEM
                FROM SCRIPTVERSAOMRD vm
                JOIN SCRIPTVERSOES sv ON vm.CODVERSAO = sv.CODVERSAO
                JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
                WHERE vm.CODVERSAO = @codVersao AND vm.PADRAO = 'T'
                ORDER BY vm.CODVERSAOMRD", new { codVersao });
        }
        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        var bytes = BlobToBytes(d["ARQUIVO_MRD"]);
        if (bytes == null || bytes.Length == 0) return null;
        var nomeArquivo = d["NOME_ARQUIVO"]?.ToString();
        var nomeScript = d["NOME"]?.ToString() ?? "script";
        var numero = d["NUMERO_VERSAO"]?.ToString() ?? "";
        var ext = MrdExtensionForScript(d["LINGUAGEM"]?.ToString(), d["SISTEMA"]?.ToString());
        var downloadName = !string.IsNullOrWhiteSpace(nomeArquivo) && nomeArquivo.Contains('.')
            ? nomeArquivo
            : $"{nomeScript}_{numero}.{ext}";
        var mime = ext == "json" ? "application/json" : "application/octet-stream";
        return (bytes, downloadName, mime);
    }

    public async Task<(byte[] content, string filename)?> ExportVersaoMrdZipAsync(int codVersao)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var meta = await conn.QueryFirstOrDefaultAsync(@"
            SELECT sv.NUMERO_VERSAO, s.NOME FROM SCRIPTVERSOES sv
            JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
            WHERE sv.CODVERSAO = @codVersao", new { codVersao });
        if (meta == null) return null;
        var md = (IDictionary<string, object>)meta;
        var mrdList = await ListVersaoMrdAsync(conn, codVersao);
        if (mrdList.Count == 0) return null;

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var m in mrdList)
            {
                var blob = await conn.ExecuteScalarAsync<object>(@"
                    SELECT ARQUIVO_MRD FROM SCRIPTVERSAOMRD
                    WHERE CODVERSAOMRD = @id AND CODVERSAO = @codVersao",
                    new { id = m.CodVersaoMrd, codVersao });
                var data = BlobToBytes(blob);
                if (data == null || data.Length == 0) continue;
                var entry = zip.CreateEntry(m.NomeArquivo, CompressionLevel.Optimal);
                await using var es = entry.Open();
                await es.WriteAsync(data);
            }
        }
        var safe = string.Concat((md["NOME"]?.ToString() ?? "script")
            .Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' ? c : '_')).Trim().Replace(' ', '_');
        return (ms.ToArray(), $"{safe}_v{md["NUMERO_VERSAO"]}_mrd.zip");
    }

    public async Task<(byte[] content, string filename, string mime)?> DownloadVersaoAnexoAsync(int codArquivo)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT TIPO, CAMINHO, NOME_ARQUIVO FROM SCRIPTVERSAOARQUIVOS WHERE CODARQUIVO = @id",
            new { id = codArquivo });
        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        var path = ResolveLegacyPath(d["CAMINHO"]?.ToString());
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path);
        var nome = d["NOME_ARQUIVO"]?.ToString() ?? "anexo";
        var tipo = d["TIPO"]?.ToString()?.ToUpperInvariant();
        var mime = tipo switch
        {
            "PDF" => "application/pdf",
            "IMAGEM" when nome.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => "image/png",
            "IMAGEM" when nome.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) => "image/gif",
            "IMAGEM" => "image/jpeg",
            _ => "application/octet-stream"
        };
        return (bytes, nome, mime);
    }

    public async Task DeleteVersaoAnexoAsync(int codArquivo)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CAMINHO FROM SCRIPTVERSAOARQUIVOS WHERE CODARQUIVO = @id", new { id = codArquivo });
        if (row == null) throw new InvalidOperationException("Arquivo não encontrado.");
        var caminho = ((IDictionary<string, object>)row)["CAMINHO"]?.ToString();
        await conn.ExecuteAsync("DELETE FROM SCRIPTVERSAOARQUIVOS WHERE CODARQUIVO = @id", new { id = codArquivo });
        if (!string.IsNullOrWhiteSpace(caminho))
        {
            var path = ResolveLegacyPath(caminho);
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (Exception ex) { _logger.LogWarning(ex, "Erro ao excluir anexo físico {Path}", path); }
            }
        }
    }

    private async Task InsertVersaoMrdAsync(FbConnection conn, FbTransaction tx, int codVersao,
        string nomeArquivo, byte[] content, bool padrao)
    {
        if (padrao)
            await conn.ExecuteAsync(
                "UPDATE SCRIPTVERSAOMRD SET PADRAO = 'F' WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await conn.ExecuteAsync(@"
            INSERT INTO SCRIPTVERSAOMRD (CODVERSAO, NOME_ARQUIVO, ARQUIVO_MRD, PADRAO)
            VALUES (@codVersao, @nome, @blob, @padrao)",
            new { codVersao, nome = nomeArquivo, blob = content, padrao = padrao ? "T" : "F" }, tx);
    }

    private static async Task CopyScriptMrdToVersaoAsync(FbConnection conn, FbTransaction tx, int scriptId, int codVersao)
    {
        var rows = await conn.QueryAsync(@"
            SELECT NOME_ARQUIVO, ARQUIVO_MRD, PADRAO FROM SCRIPTLAUDOMRD
            WHERE CODSCRIPTLAUDO = @scriptId
            ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, ORDEM, CODSCRIPTMRD", new { scriptId });
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object>)r;
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPTVERSAOMRD (CODVERSAO, NOME_ARQUIVO, ARQUIVO_MRD, PADRAO)
                VALUES (@codVersao, @nome, @blob, @padrao)",
                new
                {
                    codVersao,
                    nome = d["NOME_ARQUIVO"],
                    blob = d["ARQUIVO_MRD"],
                    padrao = (d["PADRAO"]?.ToString() ?? "F").Trim().ToUpperInvariant() == "T" ? "T" : "F"
                }, tx);
        }
    }

    private static async Task SetVersaoMrdPadraoAsync(FbConnection conn, FbTransaction tx, int codVersao, int codVersaoMrd)
    {
        await conn.ExecuteAsync(
            "UPDATE SCRIPTVERSAOMRD SET PADRAO = 'F' WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await conn.ExecuteAsync(@"
            UPDATE SCRIPTVERSAOMRD SET PADRAO = 'T'
            WHERE CODVERSAOMRD = @id AND CODVERSAO = @codVersao",
            new { id = codVersaoMrd, codVersao }, tx);
    }

    private static async Task InsertVersaoHistoricoAsync(FbConnection conn, FbTransaction tx, int codVersao,
        string tipo, string descricao, string? usuario)
    {
        await conn.ExecuteAsync(@"
            INSERT INTO SCRIPTVERSAOHISTORICO (CODVERSAO_DESTINO, TIPO_ALTERACAO, DESCRICAO, USUARIO)
            VALUES (@codVersao, @tipo, @desc, @user)",
            new { codVersao, tipo, desc = descricao, user = usuario }, tx);
    }

    private async Task<(string path, string name)> SaveVersaoDiskFileAsync(UploadedFile file, string folder, string prefix)
    {
        var ext = Path.GetExtension(file.FileName);
        var filename = $"{prefix}_{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(_staticUploadsRoot, folder);
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, filename);
        await File.WriteAllBytesAsync(full, file.Content);
        return ($"/static/uploads/{folder}/{filename}", file.FileName);
    }

    private static byte[]? BlobToBytes(object? blob)
    {
        if (blob is null or DBNull) return null;
        if (blob is byte[] bytes) return bytes;
        if (blob is Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        try { return Convert.FromBase64String(blob.ToString()!); }
        catch { return Encoding.UTF8.GetBytes(blob.ToString()!); }
    }

    private static string MrdExtensionForScript(string? linguagem, string? sistema)
    {
        if (!string.IsNullOrWhiteSpace(linguagem) && linguagem.Contains("HTML", StringComparison.OrdinalIgnoreCase))
            return "json";
        if (sistema == "Laudos UX") return "json";
        return "mrd";
    }

    public async Task<byte[]?> ExportJsonAsync(int id)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<byte[]?>(
            "SELECT ARQUIVO_JSON FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @id", new { id });
    }

    public async Task<byte[]?> ExportDllAsync(int id)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<byte[]?>(
            "SELECT DLL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @id", new { id });
    }

    public async Task<(byte[] content, string filename)?> ExportMrdAsync(int id, int? codScriptMrd)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        if (codScriptMrd.HasValue)
        {
            var row = await conn.QueryFirstOrDefaultAsync(@"
                SELECT NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTLAUDOMRD
                WHERE CODSCRIPTMRD = @m AND CODSCRIPTLAUDO = @id", new { m = codScriptMrd, id });
            if (row == null) return null;
            var d = (IDictionary<string, object>)row;
            return ((byte[])d["ARQUIVO_MRD"], d["NOME_ARQUIVO"]?.ToString() ?? "arquivo.mrd");
        }
        var padrao = await conn.QueryFirstOrDefaultAsync(@"
            SELECT NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTLAUDOMRD
            WHERE CODSCRIPTLAUDO = @id AND PADRAO = 'T' ROWS 1", new { id });
        if (padrao == null) return null;
        var p = (IDictionary<string, object>)padrao;
        return ((byte[])p["ARQUIVO_MRD"], p["NOME_ARQUIVO"]?.ToString() ?? "arquivo.mrd");
    }

    public async Task<string?> GetAzureUrlAsync(int id) =>
        (await GetScriptAsync(id))?.CaminhoAzure;

    public async Task<ApprovalTokenInfo?> GetApprovalInfoAsync(string token)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT sat.USADO, sl.APROVADO, sl.NOME, sl.SISTEMA, sl.LINGUAGEM, sl.LINK_TESTE, sat.EXPIRA_EM
            FROM SCRIPTAPROVACAOTOKEN sat
            JOIN SCRIPTLAUDO sl ON sat.CODSCRIPTLAUDO = sl.CODSCRIPTLAUDO
            WHERE sat.TOKEN = @token AND sat.EXPIRA_EM > CURRENT_TIMESTAMP", new { token });
        if (row == null) return null;
        var d = (IDictionary<string, object>)row;
        return new ApprovalTokenInfo(
            d["NOME"]!.ToString()!,
            d["SISTEMA"]!.ToString()!,
            d["LINGUAGEM"]?.ToString(),
            d["LINK_TESTE"]?.ToString(),
            d["EXPIRA_EM"] as DateTime?,
            Convert.ToInt32(d["APROVADO"]) == 1,
            Convert.ToInt32(d["USADO"]) == 1
        );
    }

    public async Task ApproveByTokenAsync(string token, bool approve)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var cod = await conn.ExecuteScalarAsync<int?>(@"
            SELECT CODSCRIPTLAUDO FROM SCRIPTAPROVACAOTOKEN
            WHERE TOKEN = @token AND EXPIRA_EM > CURRENT_TIMESTAMP AND USADO = 0", new { token });
        if (!cod.HasValue) throw new InvalidOperationException("Token inválido ou expirado.");
        await using var tx = await conn.BeginTransactionAsync();
        if (approve)
        {
            await conn.ExecuteAsync(@"
                UPDATE SCRIPTLAUDO SET APROVADO = 1, APROVADO_POR = 'Aprovado via Email', DATA_VERIFICACAO = CURRENT_TIMESTAMP
                WHERE CODSCRIPTLAUDO = @cod", new { cod }, tx);
            await conn.ExecuteAsync(@"
                UPDATE SCRIPTAPROVACAOTOKEN SET USADO = 1, APROVADO_EM = CURRENT_TIMESTAMP WHERE TOKEN = @token",
                new { token }, tx);
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE SCRIPTAPROVACAOTOKEN SET USADO = 1, REJEITADO_EM = CURRENT_TIMESTAMP WHERE TOKEN = @token",
                new { token }, tx);
        }
        await tx.CommitAsync();
    }

    public async Task<string> GetEmailsNotificacaoAsync()
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<string>(
                   "SELECT VALOR FROM CONFIGURACOES WHERE CHAVE = 'EMAILS_NOTIFICACAO_SCRIPTS'") ?? "";
    }

    public async Task SaveEmailsNotificacaoAsync(string emails)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var exists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM CONFIGURACOES WHERE CHAVE = 'EMAILS_NOTIFICACAO_SCRIPTS' ROWS 1");
        if (exists.HasValue)
            await conn.ExecuteAsync(
                "UPDATE CONFIGURACOES SET VALOR = @v WHERE CHAVE = 'EMAILS_NOTIFICACAO_SCRIPTS'", new { v = emails });
        else
            await conn.ExecuteAsync(
                "INSERT INTO CONFIGURACOES (CHAVE, VALOR) VALUES ('EMAILS_NOTIFICACAO_SCRIPTS', @v)", new { v = emails });
    }

    public async Task SendImagesEmailAsync(int codScriptLaudo, string recipientEmail, string sistema)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var script = await conn.QueryFirstOrDefaultAsync(@"
            SELECT NOME, SISTEMA FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @cod",
            new { cod = codScriptLaudo });
        if (script == null) throw new InvalidOperationException("Script não encontrado.");
        var sd = (IDictionary<string, object>)script;
        var dbSistema = sd["SISTEMA"]?.ToString() ?? "";
        if (!string.Equals(dbSistema, sistema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sistema inválido para este script.");

        var scriptName = $"{sd["NOME"]?.ToString() ?? "Script"} - {dbSistema}";
        var images = (await conn.QueryAsync<(string Caminho, string NomeArquivo)>(@"
            SELECT CAMINHO AS Caminho, NOME_ARQUIVO AS NomeArquivo
            FROM SCRIPTARQUIVOS
            WHERE CODSCRIPTLAUDO = @cod AND TIPO = 'IMAGEM'", new { cod = codScriptLaudo })).ToList();
        if (images.Count == 0) throw new InvalidOperationException("Nenhuma imagem encontrada para este script.");

        using var msg = BuildBaseMail(recipientEmail, $"Imagens do Script: {scriptName}");
        var html = $@"<p>Olá,</p>
<p>Em anexo, seguem as imagens vinculadas ao script <strong>{scriptName}</strong>.</p>
<p>Atenciosamente,<br/>Equipe Medware</p>";
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));
        AttachLogo(msg);
        foreach (var image in images)
        {
            var fullPath = ResolveLegacyPath(image.Caminho);
            if (!File.Exists(fullPath)) continue;
            msg.Attachments.Add(new Attachment(fullPath) { Name = image.NomeArquivo });
        }
        using var smtp = BuildSmtpClient();
        smtp.Send(msg);
    }

    public async Task<int> SendApprovalEmailAsync(
        int codScriptLaudo,
        string nomeScript,
        string linkTeste,
        IEnumerable<string> emails,
        string? mensagemAdicional)
    {
        var cleanEmails = emails.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (cleanEmails.Count == 0) throw new InvalidOperationException("Nenhum email informado.");
        var token = Guid.NewGuid().ToString("N");
        var expiraEm = DateTime.Now.AddDays(7);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO SCRIPTAPROVACAOTOKEN (CODSCRIPTLAUDO, TOKEN, CRIADO_EM, EXPIRA_EM)
            VALUES (@cod, @token, CURRENT_TIMESTAMP, @exp)",
            new { cod = codScriptLaudo, token, exp = expiraEm });

        var baseUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("BASE_URL"),
            _config["BASE_URL"],
            "http://localhost:3000")!;
        var approveUrl = $"{baseUrl.TrimEnd('/')}/scripts/aprovar/{token}";
        var sent = 0;
        foreach (var to in cleanEmails)
        {
            using var msg = BuildBaseMail(to!, $"Solicitação de Aprovação: {nomeScript}");
            var extra = string.IsNullOrWhiteSpace(mensagemAdicional) ? "" : $"<p><strong>Mensagem adicional:</strong> {mensagemAdicional}</p>";
            var html = $@"<p>Olá,</p>
<p>O modelo de laudo <strong>{nomeScript}</strong> foi finalizado e está aguardando aprovação.</p>
{extra}
<p><strong>Link para teste:</strong> <a href=""{linkTeste}"">{linkTeste}</a></p>
<p><a href=""{approveUrl}"">Aprovar modelo</a></p>
<p>Este link expira em 7 dias.</p>";
            msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));
            AttachLogo(msg);
            using var smtp = BuildSmtpClient();
            smtp.Send(msg);
            sent++;
        }
        return sent;
    }

    private MailMessage BuildBaseMail(string to, string subject)
    {
        var from = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SMTP_SENDER"),
            Environment.GetEnvironmentVariable("SMTP_USERNAME"),
            _config["SMTP_SENDER"],
            _config["SMTP_USERNAME"]);
        if (string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configuração SMTP ausente (SMTP_SENDER/SMTP_USERNAME).");
        var msg = new MailMessage(from, to, subject, "")
        {
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        return msg;
    }

    private SmtpClient BuildSmtpClient()
    {
        var host = FirstNonEmpty(Environment.GetEnvironmentVariable("SMTP_SERVER"), _config["SMTP_SERVER"]);
        var portText = FirstNonEmpty(Environment.GetEnvironmentVariable("SMTP_PORT"), _config["SMTP_PORT"]);
        var user = FirstNonEmpty(Environment.GetEnvironmentVariable("SMTP_USERNAME"), _config["SMTP_USERNAME"]);
        var pass = FirstNonEmpty(Environment.GetEnvironmentVariable("SMTP_PASSWORD"), _config["SMTP_PASSWORD"]);
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException("Configuração SMTP incompleta (SMTP_SERVER/SMTP_USERNAME/SMTP_PASSWORD).");
        var port = 587;
        if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsed)) port = parsed;
        var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };
        return client;
    }

    private void AttachLogo(MailMessage msg)
    {
        var logoPath = Path.Combine(MigrationRootResolver.StaticRoot(_migrationRoot), "img", "logo.png");
        if (!File.Exists(logoPath)) return;
        var logo = new LinkedResource(logoPath) { ContentId = "logo" };
        var htmlView = AlternateView.CreateAlternateViewFromString(
            "<div style='text-align:center;margin-bottom:20px'><img src='cid:logo' style='max-width:150px' /></div>",
            Encoding.UTF8,
            "text/html");
        htmlView.LinkedResources.Add(logo);
        msg.AlternateViews.Insert(0, htmlView);
    }

    private string ResolveLegacyPath(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return "";
        var raw = caminho.Trim().Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(raw)) return Path.GetFullPath(raw);

        var cleaned = raw.TrimStart(Path.DirectorySeparatorChar);
        var primary = Path.Combine(_migrationRoot, cleaned);
        if (File.Exists(primary)) return primary;
        if (_legacyRoot is not null)
        {
            var legacy = Path.Combine(_legacyRoot, cleaned);
            if (File.Exists(legacy)) return legacy;
        }
        return primary;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }
}

public class ScriptFormInput
{
    public string Nome { get; set; } = "";
    public int? CodPacote { get; set; }
    public string? Descricao { get; set; }
    public string? Linguagem { get; set; }
    public string? CaminhoProjeto { get; set; }
    public string? CaminhoAzure { get; set; }
    public string? LinkTeste { get; set; }
    public string Sistema { get; set; } = "";
    public int Aprovado { get; set; }
    public string? AprovadoPor { get; set; }
    public int Ativo { get; set; } = 1;
    public string CriadoPor { get; set; } = "";
    public byte[]? ArquivoJson { get; set; }
    public byte[]? ArquivoDll { get; set; }
    public IList<UploadedFile>? MrdFiles { get; set; }
    public IList<UploadedFile>? Imagens { get; set; }
    public IList<UploadedFile>? Pdfs { get; set; }
}

public class UploadedFile
{
    public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = [];
}

public class VersaoFormInput
{
    public string NumeroVersao { get; set; } = "";
    public string DescricaoAlteracoes { get; set; } = "";
    public string? AlteracoesInterface { get; set; }
    public string? AlteracoesCodigo { get; set; }
    public string? Observacoes { get; set; }
    public string UsuarioResponsavel { get; set; } = "";
    public byte[]? ArquivoJson { get; set; }
    public byte[]? ArquivoDll { get; set; }
    public IList<UploadedFile>? MrdFiles { get; set; }
    public IList<UploadedFile>? Imagens { get; set; }
    public IList<UploadedFile>? Pdfs { get; set; }
    public int? MrdPadraoIdx { get; set; }
    public int? MrdPadraoCod { get; set; }
    public IList<int>? MrdExcluir { get; set; }
}
