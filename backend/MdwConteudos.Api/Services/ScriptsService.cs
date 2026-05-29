using System.Data;
using System.Net;
using System.Net.Mail;
using System.Text;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Models;

namespace MdwConteudos.Api.Services;

public class ScriptsService
{
    private readonly string _connectionString;
    private readonly string _uploadRoot;
    private readonly string _migracaoStaticUploadsRoot;
    private readonly string _repoRoot;
    private readonly IConfiguration _config;
    private readonly ILogger<ScriptsService> _logger;

    public ScriptsService(IConfiguration config, ILogger<ScriptsService> logger)
    {
        _config = config;
        _connectionString = Infrastructure.EnvFileLoader.GetFirebirdConnectionString(config);
        _logger = logger;
        var repo = config["LegacyPaths:RepoRoot"];
        if (string.IsNullOrWhiteSpace(repo))
        {
            repo = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."));
        }
        _repoRoot = repo;
        _uploadRoot = Path.Combine(repo, "uploads");
        _migracaoStaticUploadsRoot = Path.Combine(repo, "mdw-migracao", "static", "uploads");
        Directory.CreateDirectory(_uploadRoot);
        Directory.CreateDirectory(_migracaoStaticUploadsRoot);
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
                CASE WHEN EXISTS (SELECT 1 FROM SCRIPTLAUDO_MRD m WHERE m.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO) THEN 1 ELSE 0 END AS TEM_MRD,
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
                FROM SCRIPT_ARQUIVOS WHERE CODSCRIPTLAUDO = @cod ORDER BY NOME_ARQUIVO", new { cod })).ToList();

            int? codVersao = null;
            string? ultimaVersao = null;
            var versaoRow = await conn.QueryFirstOrDefaultAsync(@"
                SELECT CODVERSAO, NUMERO_VERSAO FROM SCRIPT_VERSOES
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
                    FROM SCRIPT_VERSAO_ARQUIVOS WHERE CODVERSAO = @cod ORDER BY DATA_UPLOAD DESC",
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
            SELECT CODSCRIPTMRD, NOME_ARQUIVO, PADRAO, ORDEM FROM SCRIPTLAUDO_MRD
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
            FROM SCRIPT_ARQUIVOS WHERE CODSCRIPTLAUDO = @id", new { id })).ToList();
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
                INSERT INTO SCRIPT_ARQUIVOS (CODSCRIPTLAUDO, TIPO, CAMINHO, NOME_ARQUIVO)
                VALUES (@cod, 'IMAGEM', @path, @name)", new { cod, path, name }, tx);
        }
        foreach (var pdf in input.Pdfs ?? [])
        {
            var (path, name) = await SaveDiskFileAsync(pdf, "impressoes");
            await conn.ExecuteAsync(@"
                INSERT INTO SCRIPT_ARQUIVOS (CODSCRIPTLAUDO, TIPO, CAMINHO, NOME_ARQUIVO)
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
                INSERT INTO SCRIPTLAUDO_MRD (CODSCRIPTLAUDO, NOME_ARQUIVO, ARQUIVO_MRD, PADRAO, ORDEM)
                VALUES (@cod, @nome, @blob, @padrao, @ordem)",
                new { cod, nome = f.FileName, blob = f.Content, padrao, ordem }, tx);
            ordem++;
        }
    }

    private async Task<(string path, string name)> SaveDiskFileAsync(UploadedFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName);
        var filename = $"{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(_migracaoStaticUploadsRoot, folder);
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
            "SELECT COUNT(*) FROM SCRIPTLAUDO_MRD WHERE CODSCRIPTLAUDO = @cod", new { cod }, tx);
        await SaveMrdFilesAsync(conn, tx, cod, sistema, files, isFirst: has == 0);
        await tx.CommitAsync();
    }

    public async Task DeleteMrdAsync(int codScriptMrd)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM SCRIPTLAUDO_MRD WHERE CODSCRIPTMRD = @cod", new { cod = codScriptMrd });
    }

    public async Task<IReadOnlyList<VersaoDto>> ListVersoesAsync(int scriptId)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync(@"
            SELECT CODVERSAO, NUMERO_VERSAO, DATA_CRIACAO, ATIVO, APROVADO, OBSERVACOES
            FROM SCRIPT_VERSOES WHERE CODSCRIPTLAUDO = @scriptId ORDER BY DATA_CRIACAO DESC",
            new { scriptId });
        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            return new VersaoDto(
                Convert.ToInt32(d["CODVERSAO"]),
                d["NUMERO_VERSAO"]?.ToString() ?? "",
                d["DATA_CRIACAO"] as DateTime?,
                d["ATIVO"]?.ToString(),
                d["APROVADO"] as int?,
                d["OBSERVACOES"]?.ToString()
            );
        }).ToList();
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
            FROM SCRIPT_VERSOES sv
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
            d["APROVADO"]?.ToString(),
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
                FROM SCRIPT_VERSAO_ARQUIVOS
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
            FROM SCRIPT_VERSAO_MRD
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
                FROM SCRIPT_VERSAO_HISTORICO
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

    public async Task<int> CreateVersaoAsync(int scriptId, string numeroVersao, string? observacoes, string criadoPor)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO SCRIPT_VERSOES (CODSCRIPTLAUDO, NUMERO_VERSAO, OBSERVACOES, CRIADO_POR, ATIVO, APROVADO, DATA_CRIACAO)
            VALUES (@scriptId, @num, @obs, @criado, 'F', 0, CURRENT_TIMESTAMP)
            RETURNING CODVERSAO",
            new { scriptId, num = numeroVersao, obs = observacoes, criado = criadoPor });
    }

    public async Task ActivateVersaoAsync(int codVersao)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var scriptId = await conn.ExecuteScalarAsync<int>(
            "SELECT CODSCRIPTLAUDO FROM SCRIPT_VERSOES WHERE CODVERSAO = @codVersao", new { codVersao });
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(
            "UPDATE SCRIPT_VERSOES SET ATIVO = 'F' WHERE CODSCRIPTLAUDO = @scriptId", new { scriptId }, tx);
        await conn.ExecuteAsync(
            "UPDATE SCRIPT_VERSOES SET ATIVO = 'T' WHERE CODVERSAO = @codVersao", new { codVersao }, tx);
        await tx.CommitAsync();
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
                SELECT NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTLAUDO_MRD
                WHERE CODSCRIPTMRD = @m AND CODSCRIPTLAUDO = @id", new { m = codScriptMrd, id });
            if (row == null) return null;
            var d = (IDictionary<string, object>)row;
            return ((byte[])d["ARQUIVO_MRD"], d["NOME_ARQUIVO"]?.ToString() ?? "arquivo.mrd");
        }
        var padrao = await conn.QueryFirstOrDefaultAsync(@"
            SELECT NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTLAUDO_MRD
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
            FROM SCRIPT_APROVACAO_TOKEN sat
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
            SELECT CODSCRIPTLAUDO FROM SCRIPT_APROVACAO_TOKEN
            WHERE TOKEN = @token AND EXPIRA_EM > CURRENT_TIMESTAMP AND USADO = 0", new { token });
        if (!cod.HasValue) throw new InvalidOperationException("Token inválido ou expirado.");
        await using var tx = await conn.BeginTransactionAsync();
        if (approve)
        {
            await conn.ExecuteAsync(@"
                UPDATE SCRIPTLAUDO SET APROVADO = 1, APROVADO_POR = 'Aprovado via Email', DATA_VERIFICACAO = CURRENT_TIMESTAMP
                WHERE CODSCRIPTLAUDO = @cod", new { cod }, tx);
            await conn.ExecuteAsync(@"
                UPDATE SCRIPT_APROVACAO_TOKEN SET USADO = 1, APROVADO_EM = CURRENT_TIMESTAMP WHERE TOKEN = @token",
                new { token }, tx);
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE SCRIPT_APROVACAO_TOKEN SET USADO = 1, REJEITADO_EM = CURRENT_TIMESTAMP WHERE TOKEN = @token",
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
            FROM SCRIPT_ARQUIVOS
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
            INSERT INTO SCRIPT_APROVACAO_TOKEN (CODSCRIPTLAUDO, TOKEN, CRIADO_EM, EXPIRA_EM)
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
        var logoPath = Path.Combine(_repoRoot, "static", "img", "logo.png");
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
        var cleaned = caminho.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var primary = Path.Combine(_repoRoot, cleaned);
        if (File.Exists(primary)) return primary;
        var secondary = Path.Combine(_repoRoot, "mdw-migracao", cleaned);
        if (File.Exists(secondary)) return secondary;
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
