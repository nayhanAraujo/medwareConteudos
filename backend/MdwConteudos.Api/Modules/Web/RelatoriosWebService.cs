using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Dapper;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;

namespace MdwConteudos.Api.Modules.Web;

public interface IRelatoriosWebService
{
    Task<object> ListAsync(string? search, string? modulo, string? formato, string? status, string? multiselecao, int page, int perPage, CancellationToken ct);
    Task<object?> GetAsync(int id, CancellationToken ct);
    Task<object> CreateAsync(RelatorioUpsertRequest req, int? codUsuario, CancellationToken ct);
    Task UpdateAsync(int id, RelatorioUpsertRequest req, CancellationToken ct);
    Task SetStatusAsync(int id, object ativo, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<(byte[] Bytes, string FileName)> DownloadAsync(int id, CancellationToken ct);
    Task<(byte[] Bytes, string FileName)> ExportZipAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<object> ImportLoteAsync(string modulo, int ativo, IReadOnlyList<(string FileName, string Conteudo)> arquivos, int? codUsuario, CancellationToken ct);
    Task<object> ListSistemasAsync(CancellationToken ct);
    Task<object> ListModulosAsync(int? codSistema, CancellationToken ct);
    Task<object> ListModulosSimplesAsync(int? codSistema, CancellationToken ct);
    Task<object> CreateModuloAsync(ModuloRelatorioCreateRequest req, int codUsuario, CancellationToken ct);
    Task UpdateModuloAsync(string nomeModulo, ModuloRelatorioUpdateRequest req, int codUsuario, CancellationToken ct);
    Task DeleteModuloAsync(string nomeModulo, int codUsuario, CancellationToken ct);
}

public class RelatoriosWebService : IRelatoriosWebService
{
    private static readonly string[] FallbackModulos =
    {
        "Agenda", "Caixa", "Bibliotecas", "Faturamento", "Medicos", "Financeiro", "Estoque", "Nota Fiscal"
    };

    private const string MarkMultiselChoise = "<TYPE>FT_LISTCODE_MULTIPLECHOISE</TYPE>";
    private const string MarkMultiselChoice = "<TYPE>FT_LISTCODE_MULTIPLECHOICE</TYPE>";

    private readonly IFirebirdConnectionFactory _db;

    public RelatoriosWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListAsync(
        string? search, string? modulo, string? formato, string? status, string? multiselecao,
        int page, int perPage, CancellationToken ct)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        var offset = (page - 1) * perPage;

        var where = new List<string>();
        var p = new DynamicParameters();
        p.Add("take", perPage);
        p.Add("skip", offset);

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(UPPER(NOME) LIKE UPPER(@search) OR UPPER(MODULO) LIKE UPPER(@search))");
            p.Add("search", $"%{search.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            where.Add("MODULO = @modulo");
            p.Add("modulo", modulo.Trim());
        }
        if (!string.IsNullOrWhiteSpace(formato))
        {
            where.Add("FORMATO = @formato");
            p.Add("formato", formato.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusVal = ParseStatusQuery(status);
            if (statusVal is null)
                throw new InvalidOperationException("Parâmetro status inválido (use 1 ou 0)");
            where.Add("ATIVO = @status");
            p.Add("status", statusVal.Value);
        }
        if (!string.IsNullOrWhiteSpace(multiselecao))
        {
            var ms = ParseMultiselecaoQuery(multiselecao);
            if (ms is null)
                throw new InvalidOperationException("Parâmetro multiselecao inválido (use 1 ou 0)");
            where.Add("TEM_MULTISELECAO = @ms");
            p.Add("ms", ms.Value);
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var conn = await _db.OpenConnectionAsync(ct);
        var temValidacoes = await TableExistsAsync(conn, "RELATORIOVALIDACOES");
        var validacaoSelect = temValidacoes
            ? @", CASE WHEN EXISTS (
                    SELECT 1 FROM RELATORIOVALIDACOES rv WHERE rv.CODRELATORIO = RELATORIOS.CODRELATORIO
                  ) THEN 1 ELSE 0 END AS TEM_VALIDACAO"
            : ", 0 AS TEM_VALIDACAO";

        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM RELATORIOS {whereClause}", p);
        var rows = await conn.QueryAsync($@"
            SELECT FIRST @take SKIP @skip
                   CODRELATORIO, NOME, MODULO, FORMATO, DTHRCRIACAO, ATIVO, TEM_MULTISELECAO
                   {validacaoSelect}
            FROM RELATORIOS
            {whereClause}
            ORDER BY DTHRCRIACAO DESC", p);

        var list = rows.Select(MapListItem).ToList();
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)perPage);
        return new
        {
            success = true,
            data = list,
            page,
            perPage,
            totalPages = pages,
            totalItems = total
        };
    }

    public async Task<object?> GetAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODRELATORIO, NOME, MODULO, FORMATO, CONTEUDO, DTHRCRIACAO, ATIVO, TEM_MULTISELECAO
            FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
        if (row is null) return null;

        return new
        {
            codrelatorio = (int)row.CODRELATORIO,
            nome = (string?)row.NOME,
            modulo = (string?)row.MODULO,
            formato = (string?)row.FORMATO,
            conteudo = BlobToUtf8(row.CONTEUDO),
            dthrcriacao = FormatDate(row.DTHRCRIACAO),
            ativo = ToInt01(row.ATIVO),
            tem_multiselecao = ToBool(row.TEM_MULTISELECAO)
        };
    }

    public async Task<object> CreateAsync(RelatorioUpsertRequest req, int? codUsuario, CancellationToken ct)
    {
        var (nome, modulo, formato, conteudo, ativo) = ValidateUpsert(req);
        if (nome.Length > 200) nome = nome[..200];
        var temMs = TemMultiselecaoFromConteudo(conteudo);
        var conteudoUtf8 = Encoding.UTF8.GetBytes(conteudo);

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE NOME = @nome AND MODULO = @modulo",
            new { nome, modulo });
        if (exists > 0)
            throw new InvalidOperationException($"Já existe um relatório com o nome \"{nome}\" no módulo \"{modulo}\"");

        // Alinhado ao legado: INSERT + SELECT (sem depender de RETURNING) e conteúdo em UTF-8.
        Exception? last = null;
        foreach (var attempt in BuildInsertAttempts(nome, modulo, formato, conteudo, conteudoUtf8, ativo, temMs))
        {
            try
            {
                await conn.ExecuteAsync(attempt.Sql, attempt.Params);
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        if (last is not null)
            throw new InvalidOperationException($"Falha ao gravar relatório: {last.Message}", last);

        var cod = await conn.ExecuteScalarAsync<int>(@"
            SELECT FIRST 1 CODRELATORIO FROM RELATORIOS
            WHERE NOME = @nome AND MODULO = @modulo
            ORDER BY CODRELATORIO DESC",
            new { nome, modulo });
        if (cod <= 0)
            throw new InvalidOperationException("Relatório gravado, mas não foi possível obter o código.");

        if (formato == "XML")
        {
            try { await IndexarColunasAsync(conn, cod, conteudo, codUsuario); }
            catch { /* indexação não deve bloquear o cadastro */ }
        }

        try
        {
            var created = await GetAsync(cod, ct);
            return new { success = true, message = "Relatório cadastrado com sucesso", relatorio = created, codRelatorio = cod };
        }
        catch
        {
            return new { success = true, message = "Relatório cadastrado com sucesso", relatorio = (object?)null, codRelatorio = cod };
        }
    }

    public async Task UpdateAsync(int id, RelatorioUpsertRequest req, CancellationToken ct)
    {
        var (nome, modulo, formato, conteudo, ativo) = ValidateUpsert(req);
        var temMs = TemMultiselecaoFromConteudo(conteudo);
        var conteudoUtf8 = Encoding.UTF8.GetBytes(conteudo);

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
        if (exists == 0)
            throw new InvalidOperationException("Relatório não encontrado");

        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE NOME = @nome AND MODULO = @modulo AND CODRELATORIO <> @id",
            new { nome, modulo, id });
        if (dup > 0)
            throw new InvalidOperationException($"Já existe outro relatório com o nome \"{nome}\" no módulo \"{modulo}\"");

        Exception? last = null;
        foreach (var attempt in BuildUpdateAttempts(id, nome, modulo, formato, conteudo, conteudoUtf8, ativo, temMs))
        {
            try
            {
                await conn.ExecuteAsync(attempt.Sql, attempt.Params);
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        if (last is not null)
            throw new InvalidOperationException($"Falha ao atualizar relatório: {last.Message}", last);
    }

    public async Task SetStatusAsync(int id, object ativoRaw, CancellationToken ct)
    {
        var ativo = ParseAtivo(ativoRaw, required: true);
        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
        if (exists == 0)
            throw new InvalidOperationException("Relatório não encontrado");

        await conn.ExecuteAsync("UPDATE RELATORIOS SET ATIVO = @ativo WHERE CODRELATORIO = @id", new { ativo, id });
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
        if (exists == 0)
            throw new InvalidOperationException("Relatório não encontrado");

        if (await TableExistsAsync(conn, "RELATORIOCOLUNAS"))
            await conn.ExecuteAsync("DELETE FROM RELATORIOCOLUNAS WHERE CODRELATORIO = @id", new { id });
        if (await TableExistsAsync(conn, "RELATORIOVALIDACOES"))
            await conn.ExecuteAsync("DELETE FROM RELATORIOVALIDACOES WHERE CODRELATORIO = @id", new { id });

        await conn.ExecuteAsync("DELETE FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
    }

    public async Task<(byte[] Bytes, string FileName)> DownloadAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT NOME, FORMATO, CONTEUDO FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
        if (row is null)
            throw new InvalidOperationException("Relatório não encontrado");

        var nome = (string?)row.NOME ?? $"relatorio_{id}";
        var formato = ((string?)row.FORMATO ?? "bin").ToLowerInvariant();
        var ansi = ToAnsiBytes(row.CONTEUDO);
        return (ansi, $"{SanitizeFileName(nome)}.{formato}");
    }

    public async Task<(byte[] Bytes, string FileName)> ExportZipAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            throw new InvalidOperationException("Nenhum relatório selecionado");

        await using var conn = await _db.OpenConnectionAsync(ct);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in idList)
            {
                var row = await conn.QueryFirstOrDefaultAsync(
                    "SELECT NOME, FORMATO, CONTEUDO FROM RELATORIOS WHERE CODRELATORIO = @id", new { id });
                if (row is null) continue;

                var nome = SanitizeFileName((string?)row.NOME ?? $"relatorio_{id}");
                var ext = ((string?)row.FORMATO ?? "bin").ToLowerInvariant();
                var entryName = $"{nome}.{ext}";
                var n = 1;
                while (!usedNames.Add(entryName))
                {
                    entryName = $"{nome}_{n++}.{ext}";
                }

                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                var bytes = ToAnsiBytes(row.CONTEUDO);
                await entryStream.WriteAsync(bytes, ct);
            }
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return (ms.ToArray(), $"relatorios_export_{stamp}.zip");
    }

    public async Task<object> ImportLoteAsync(
        string modulo, int ativo, IReadOnlyList<(string FileName, string Conteudo)> arquivos, int? codUsuario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(modulo))
            throw new InvalidOperationException("Módulo é obrigatório");
        if (arquivos.Count == 0)
            throw new InvalidOperationException("Nenhum arquivo enviado");

        var created = new List<object>();
        var errors = new List<object>();

        foreach (var (fileName, conteudo) in arquivos)
        {
            try
            {
                var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
                if (ext is not ("XML" or "JSON"))
                    throw new InvalidOperationException("Formato deve ser XML ou JSON");

                var nome = Path.GetFileNameWithoutExtension(fileName).Trim();
                if (string.IsNullOrWhiteSpace(nome))
                    throw new InvalidOperationException("Nome do arquivo inválido");

                var result = await CreateAsync(
                    new RelatorioUpsertRequest(nome, modulo.Trim(), ext, conteudo, ativo),
                    codUsuario,
                    ct);
                created.Add(result);
            }
            catch (Exception ex)
            {
                errors.Add(new { arquivo = fileName, error = ex.Message });
            }
        }

        return new
        {
            success = errors.Count == 0,
            message = $"Importados {created.Count} de {arquivos.Count} arquivo(s)",
            created = created.Count,
            errors
        };
    }

    public async Task<object> ListSistemasAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync(@"
            SELECT S.CODSISTEMA, S.NOME, S.DESCRICAO,
                   (SELECT COUNT(DISTINCT SM.CODMODULO)
                    FROM SISTEMA_MODULO SM
                    JOIN MODULORELATORIO MR ON MR.CODMODULO = SM.CODMODULO
                    WHERE SM.CODSISTEMA = S.CODSISTEMA AND MR.ATIVO = 1) AS QTD_MODULOS
            FROM SISTEMA S
            WHERE S.ATIVO = 1
            ORDER BY S.NOME");

        var sistemas = rows.Select(s => new
        {
            codsistema = (int)s.CODSISTEMA,
            nome = (string?)s.NOME,
            descricao = (string?)s.DESCRICAO ?? "",
            qtd_modulos = (int)(s.QTD_MODULOS ?? 0)
        }).ToList();

        return new { success = true, sistemas };
    }

    public async Task<object> ListModulosAsync(int? codSistema, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var tabelaExiste = await TableExistsAsync(conn, "MODULORELATORIO");

        var modulos = new List<Dictionary<string, object?>>();
        if (!tabelaExiste)
        {
            for (var i = 0; i < FallbackModulos.Length; i++)
            {
                modulos.Add(new Dictionary<string, object?>
                {
                    ["codmodulo"] = i + 1,
                    ["nome"] = FallbackModulos[i],
                    ["descricao"] = "",
                    ["ativo"] = 1,
                    ["dthrcriacao"] = null,
                    ["dthratualizacao"] = null,
                    ["sistemas"] = Array.Empty<object>(),
                    ["total_relatorios"] = 0,
                    ["ultima_atualizacao"] = null
                });
            }
        }
        else
        {
            IEnumerable<dynamic> rows;
            if (codSistema.HasValue)
            {
                rows = await conn.QueryAsync(@"
                    SELECT DISTINCT MR.CODMODULO, MR.NOME, MR.DESCRICAO, MR.ATIVO, MR.DTHRCRIACAO, MR.DTHRATUALIZACAO
                    FROM MODULORELATORIO MR
                    JOIN SISTEMA_MODULO SM ON SM.CODMODULO = MR.CODMODULO
                    WHERE MR.ATIVO = 1 AND SM.CODSISTEMA = @cs
                    ORDER BY MR.NOME", new { cs = codSistema.Value });
            }
            else
            {
                rows = await conn.QueryAsync(@"
                    SELECT CODMODULO, NOME, DESCRICAO, ATIVO, DTHRCRIACAO, DTHRATUALIZACAO
                    FROM MODULORELATORIO
                    WHERE ATIVO = 1
                    ORDER BY NOME");
            }

            foreach (var m in rows)
            {
                var cod = (int)m.CODMODULO;
                var sistemas = (await conn.QueryAsync(@"
                    SELECT S.CODSISTEMA, S.NOME
                    FROM SISTEMA_MODULO SM
                    JOIN SISTEMA S ON S.CODSISTEMA = SM.CODSISTEMA
                    WHERE SM.CODMODULO = @cod
                    ORDER BY S.NOME", new { cod }))
                    .Select(s => new { codsistema = (int)s.CODSISTEMA, nome = (string?)s.NOME })
                    .ToList();

                modulos.Add(new Dictionary<string, object?>
                {
                    ["codmodulo"] = cod,
                    ["nome"] = (string?)m.NOME,
                    ["descricao"] = (string?)m.DESCRICAO ?? "",
                    ["ativo"] = ToInt01(m.ATIVO),
                    ["dthrcriacao"] = FormatDate(m.DTHRCRIACAO),
                    ["dthratualizacao"] = FormatDate(m.DTHRATUALIZACAO),
                    ["sistemas"] = sistemas,
                    ["total_relatorios"] = 0,
                    ["ultima_atualizacao"] = null
                });
            }
        }

        foreach (var modulo in modulos)
        {
            var nome = (string)modulo["nome"]!;
            var codmodulo = Convert.ToInt32(modulo["codmodulo"]);
            int count;
            if (tabelaExiste)
            {
                count = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM RELATORIOS WHERE CODMODULO = @cod", new { cod = codmodulo });
                if (count == 0)
                    count = await conn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM RELATORIOS WHERE MODULO = @nome", new { nome });
            }
            else
            {
                count = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM RELATORIOS WHERE MODULO = @nome", new { nome });
            }

            modulo["total_relatorios"] = count;
            if (count > 0)
            {
                DateTime? ultima;
                if (tabelaExiste)
                {
                    ultima = await conn.ExecuteScalarAsync<DateTime?>(
                        "SELECT MAX(DTHRCRIACAO) FROM RELATORIOS WHERE CODMODULO = @cod", new { cod = codmodulo });
                    if (ultima is null)
                        ultima = await conn.ExecuteScalarAsync<DateTime?>(
                            "SELECT MAX(DTHRCRIACAO) FROM RELATORIOS WHERE MODULO = @nome", new { nome });
                }
                else
                {
                    ultima = await conn.ExecuteScalarAsync<DateTime?>(
                        "SELECT MAX(DTHRCRIACAO) FROM RELATORIOS WHERE MODULO = @nome", new { nome });
                }
                modulo["ultima_atualizacao"] = FormatDate(ultima);
            }
        }

        return new { success = true, modulos, tabela_existe = tabelaExiste };
    }

    public async Task<object> ListModulosSimplesAsync(int? codSistema, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var tabelaExiste = await TableExistsAsync(conn, "MODULORELATORIO");
        List<object> modulos;

        if (!tabelaExiste)
        {
            modulos = FallbackModulos.Select(n => (object)new { nome = n, descricao = "" }).ToList();
        }
        else if (codSistema.HasValue)
        {
            var rows = await conn.QueryAsync(@"
                SELECT DISTINCT MR.NOME, MR.DESCRICAO
                FROM MODULORELATORIO MR
                JOIN SISTEMA_MODULO SM ON SM.CODMODULO = MR.CODMODULO
                WHERE MR.ATIVO = 1 AND SM.CODSISTEMA = @cs
                ORDER BY MR.NOME", new { cs = codSistema.Value });
            modulos = rows.Select(r => (object)new { nome = (string?)r.NOME, descricao = (string?)r.DESCRICAO ?? "" }).ToList();
        }
        else
        {
            var rows = await conn.QueryAsync(@"
                SELECT NOME, DESCRICAO FROM MODULORELATORIO WHERE ATIVO = 1 ORDER BY NOME");
            modulos = rows.Select(r => (object)new { nome = (string?)r.NOME, descricao = (string?)r.DESCRICAO ?? "" }).ToList();
        }

        return new { success = true, modulos, tabela_existe = tabelaExiste };
    }

    public async Task<object> CreateModuloAsync(ModuloRelatorioCreateRequest req, int codUsuario, CancellationToken ct)
    {
        var nome = (req.NomeModulo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Nome do módulo é obrigatório");
        if (req.Sistemas is null || req.Sistemas.Count == 0)
            throw new InvalidOperationException("Selecione pelo menos um sistema");

        await using var conn = await _db.OpenConnectionAsync(ct);
        if (!await TableExistsAsync(conn, "MODULORELATORIO"))
            throw new InvalidOperationException("Tabela MODULORELATORIO não existe. Execute o script SQL primeiro.");

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM MODULORELATORIO WHERE UPPER(NOME) = UPPER(@nome)", new { nome });
        if (exists > 0)
            throw new InvalidOperationException("Já existe um módulo com este nome");

        var cod = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO MODULORELATORIO (NOME, DESCRICAO, USUARIOCRIACAO)
            VALUES (@nome, @descricao, @user)
            RETURNING CODMODULO",
            new { nome, descricao = req.Descricao?.Trim() ?? "", user = codUsuario });

        foreach (var cs in req.Sistemas.Distinct())
        {
            await conn.ExecuteAsync(
                "INSERT INTO SISTEMA_MODULO (CODSISTEMA, CODMODULO) VALUES (@cs, @cod)",
                new { cs, cod });
        }

        return new
        {
            success = true,
            message = $"Módulo \"{nome}\" criado com sucesso",
            modulo = new
            {
                codmodulo = cod,
                nome,
                descricao = req.Descricao?.Trim() ?? "",
                ativo = 1,
                total_relatorios = 0,
                ultima_atualizacao = (string?)null
            }
        };
    }

    public async Task UpdateModuloAsync(string nomeModulo, ModuloRelatorioUpdateRequest req, int codUsuario, CancellationToken ct)
    {
        var novoNome = (req.NovoNome ?? "").Trim();
        if (string.IsNullOrWhiteSpace(novoNome))
            throw new InvalidOperationException("Novo nome do módulo é obrigatório");

        await using var conn = await _db.OpenConnectionAsync(ct);
        if (!await TableExistsAsync(conn, "MODULORELATORIO"))
            throw new InvalidOperationException("Tabela MODULORELATORIO não existe. Execute o script SQL primeiro.");

        var cod = await conn.ExecuteScalarAsync<int?>(
            "SELECT CODMODULO FROM MODULORELATORIO WHERE UPPER(NOME) = UPPER(@nome)",
            new { nome = nomeModulo });
        if (cod is null)
            throw new InvalidOperationException("Módulo não encontrado");

        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM MODULORELATORIO WHERE UPPER(NOME) = UPPER(@novo) AND CODMODULO <> @cod",
            new { novo = novoNome, cod });
        if (dup > 0)
            throw new InvalidOperationException("Já existe um módulo com este nome");

        await conn.ExecuteAsync(@"
            UPDATE MODULORELATORIO
            SET NOME = @novo, DESCRICAO = @descricao, USUARIOATUALIZACAO = @user
            WHERE CODMODULO = @cod",
            new { novo = novoNome, descricao = req.Descricao, user = codUsuario, cod });

        if (req.Sistemas is not null)
        {
            await conn.ExecuteAsync("DELETE FROM SISTEMA_MODULO WHERE CODMODULO = @cod", new { cod });
            foreach (var cs in req.Sistemas.Distinct())
            {
                await conn.ExecuteAsync(
                    "INSERT INTO SISTEMA_MODULO (CODSISTEMA, CODMODULO) VALUES (@cs, @cod)",
                    new { cs, cod });
            }
        }
    }

    public async Task DeleteModuloAsync(string nomeModulo, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (!await TableExistsAsync(conn, "MODULORELATORIO"))
            throw new InvalidOperationException("Tabela MODULORELATORIO não existe. Execute o script SQL primeiro.");

        var cod = await conn.ExecuteScalarAsync<int?>(
            "SELECT CODMODULO FROM MODULORELATORIO WHERE UPPER(NOME) = UPPER(@nome)",
            new { nome = nomeModulo });
        if (cod is null)
            throw new InvalidOperationException("Módulo não encontrado");

        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RELATORIOS WHERE CODMODULO = @cod", new { cod });
        if (count == 0)
            count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM RELATORIOS WHERE MODULO = @nome", new { nome = nomeModulo });

        if (count > 0)
            throw new InvalidOperationException(
                $"Não é possível excluir o módulo \"{nomeModulo}\" pois existem {count} relatório(s) vinculado(s) a ele");

        await conn.ExecuteAsync(
            "UPDATE MODULORELATORIO SET ATIVO = 0, USUARIOATUALIZACAO = @user WHERE CODMODULO = @cod",
            new { user = codUsuario, cod });
    }

    private static (string nome, string modulo, string formato, string conteudo, int ativo) ValidateUpsert(RelatorioUpsertRequest req)
    {
        var nome = (req.Nome ?? "").Trim();
        var modulo = (req.Modulo ?? "").Trim();
        var formato = (req.Formato ?? "").Trim().ToUpperInvariant();
        // Não usar Trim no conteúdo XML/JSON: espaços/indentação no início podem ser relevantes;
        // só rejeita vazio.
        var conteudo = req.Conteudo ?? "";
        var ativo = ParseAtivo(req.Ativo, required: false, defaultValue: 1);

        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(modulo) ||
            string.IsNullOrWhiteSpace(formato) || string.IsNullOrWhiteSpace(conteudo))
            throw new InvalidOperationException("Todos os campos obrigatórios devem ser preenchidos");

        if (formato is not ("XML" or "JSON"))
            throw new InvalidOperationException("Formato deve ser XML ou JSON");

        if (formato == "JSON")
        {
            try { JsonDocument.Parse(conteudo); }
            catch (JsonException) { throw new InvalidOperationException("Conteúdo JSON inválido"); }
        }

        return (nome, modulo, formato, conteudo, ativo);
    }

    private static IEnumerable<(string Sql, object Params)> BuildInsertAttempts(
        string nome, string modulo, string formato, string conteudo, byte[] conteudoUtf8, int ativo, int temMs)
    {
        yield return (
            @"INSERT INTO RELATORIOS (NOME, MODULO, FORMATO, CONTEUDO, ATIVO, TEM_MULTISELECAO)
              VALUES (@nome, @modulo, @formato, @conteudo, @ativo, @temMs)",
            (object)new { nome, modulo, formato, conteudo = conteudoUtf8, ativo, temMs });
        yield return (
            @"INSERT INTO RELATORIOS (NOME, MODULO, FORMATO, CONTEUDO, ATIVO, TEM_MULTISELECAO)
              VALUES (@nome, @modulo, @formato, @conteudo, @ativo, @temMs)",
            new { nome, modulo, formato, conteudo, ativo, temMs });
        yield return (
            @"INSERT INTO RELATORIOS (NOME, MODULO, FORMATO, CONTEUDO, ATIVO, TEM_MULTISELECAO, DTHRCRIACAO)
              VALUES (@nome, @modulo, @formato, @conteudo, @ativo, @temMs, CURRENT_TIMESTAMP)",
            new { nome, modulo, formato, conteudo = conteudoUtf8, ativo, temMs });
        yield return (
            @"INSERT INTO RELATORIOS (NOME, MODULO, FORMATO, CONTEUDO, ATIVO)
              VALUES (@nome, @modulo, @formato, @conteudo, @ativo)",
            new { nome, modulo, formato, conteudo = conteudoUtf8, ativo });
    }

    private static IEnumerable<(string Sql, object Params)> BuildUpdateAttempts(
        int id, string nome, string modulo, string formato, string conteudo, byte[] conteudoUtf8, int ativo, int temMs)
    {
        yield return (
            @"UPDATE RELATORIOS
              SET NOME = @nome, MODULO = @modulo, FORMATO = @formato, CONTEUDO = @conteudo,
                  ATIVO = @ativo, TEM_MULTISELECAO = @temMs
              WHERE CODRELATORIO = @id",
            (object)new { nome, modulo, formato, conteudo = conteudoUtf8, ativo, temMs, id });
        yield return (
            @"UPDATE RELATORIOS
              SET NOME = @nome, MODULO = @modulo, FORMATO = @formato, CONTEUDO = @conteudo,
                  ATIVO = @ativo, TEM_MULTISELECAO = @temMs
              WHERE CODRELATORIO = @id",
            new { nome, modulo, formato, conteudo, ativo, temMs, id });
        yield return (
            @"UPDATE RELATORIOS
              SET NOME = @nome, MODULO = @modulo, FORMATO = @formato, CONTEUDO = @conteudo, ATIVO = @ativo
              WHERE CODRELATORIO = @id",
            new { nome, modulo, formato, conteudo = conteudoUtf8, ativo, id });
    }

    private static int TemMultiselecaoFromConteudo(string? conteudo)
    {
        if (string.IsNullOrEmpty(conteudo)) return 0;
        return conteudo.Contains(MarkMultiselChoise, StringComparison.Ordinal) ||
               conteudo.Contains(MarkMultiselChoice, StringComparison.Ordinal)
            ? 1 : 0;
    }

    private static int ParseAtivo(object? valor, bool required, int defaultValue = 1)
    {
        if (valor is null)
        {
            if (required) throw new InvalidOperationException("Campo ativo é obrigatório");
            return defaultValue;
        }
        if (valor is bool b) return b ? 1 : 0;
        if (valor is int i)
        {
            if (i is 0 or 1) return i;
            throw new InvalidOperationException("Ativo deve ser 0 ou 1");
        }
        if (valor is long l)
        {
            if (l is 0 or 1) return (int)l;
            throw new InvalidOperationException("Ativo deve ser 0 ou 1");
        }
        if (valor is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n) && n is 0 or 1) return n;
            if (je.ValueKind == JsonValueKind.True) return 1;
            if (je.ValueKind == JsonValueKind.False) return 0;
            if (je.ValueKind == JsonValueKind.String) return ParseAtivo(je.GetString(), required, defaultValue);
        }

        var s = Convert.ToString(valor)?.Trim().ToUpperInvariant() ?? "";
        if (s == "" && !required) return defaultValue;
        if (s is "1" or "S" or "TRUE" or "SIM") return 1;
        if (s is "0" or "N" or "FALSE" or "NAO" or "NÃO") return 0;
        throw new InvalidOperationException("Ativo inválido");
    }

    private static int? ParseStatusQuery(string status)
    {
        try { return ParseAtivo(status, required: true); }
        catch { return null; }
    }

    private static int? ParseMultiselecaoQuery(string multiselecao)
    {
        var s = multiselecao.Trim();
        if (s == "1") return 1;
        if (s == "0") return 0;
        return null;
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection conn, string tableName)
    {
        var count = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM RDB$RELATIONS
            WHERE TRIM(RDB$RELATION_NAME) = @name", new { name = tableName.ToUpperInvariant() });
        return count > 0;
    }

    private static async Task IndexarColunasAsync(
        System.Data.Common.DbConnection conn, int codRelatorio, string conteudoXml, int? usuario)
    {
        if (!await TableExistsAsync(conn, "RELATORIOCOLUNAS")) return;

        List<(string Nome, int Posicao)> colunas;
        try
        {
            var doc = XDocument.Parse(conteudoXml);
            colunas = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("COL", StringComparison.OrdinalIgnoreCase))
                .Select((col, idx) =>
                {
                    var text = col.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        ?.Value?.Trim();
                    return (Nome: text ?? "", Posicao: idx + 1);
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Nome))
                .ToList();
        }
        catch
        {
            return;
        }

        await conn.ExecuteAsync("DELETE FROM RELATORIOCOLUNAS WHERE CODRELATORIO = @id", new { id = codRelatorio });
        foreach (var col in colunas)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO RELATORIOCOLUNAS
                (CODRELATORIO, NOME_COLUNA, POSICAO_COLUNA, TIPO_COLUNA, ATIVO, USUARIOCRIACAO)
                VALUES (@id, @nome, @pos, 'TEXTO', 1, @user)",
                new { id = codRelatorio, nome = col.Nome, pos = col.Posicao, user = usuario });
        }
    }

    private static object MapListItem(dynamic r) => new
    {
        codrelatorio = (int)r.CODRELATORIO,
        nome = (string?)r.NOME,
        modulo = (string?)r.MODULO,
        formato = (string?)r.FORMATO,
        dthrcriacao = FormatDate(r.DTHRCRIACAO),
        ativo = ToInt01(r.ATIVO),
        tem_multiselecao = ToBool(r.TEM_MULTISELECAO),
        tem_validacao = ToBool(r.TEM_VALIDACAO)
    };

    private static string? FormatDate(object? value)
    {
        if (value is null or DBNull) return null;
        if (value is DateTime dt) return dt.ToString("dd/MM/yyyy HH:mm");
        return value.ToString();
    }

    private static int ToInt01(object? value)
    {
        if (value is null or DBNull) return 0;
        try { return Convert.ToInt32(value) != 0 ? 1 : 0; }
        catch { return 0; }
    }

    private static bool ToBool(object? value) => ToInt01(value) == 1;

    private static string BlobToUtf8(object? blob)
    {
        if (blob is null or DBNull) return "";
        // Text blobs já decodificados pelo driver: preservar string
        if (blob is string s) return s;
        var bytes = BlobHelper.ToBytes(blob);
        if (bytes is null || bytes.Length == 0) return "";
        return DecodeTextBytes(bytes);
    }

    private static string DecodeTextBytes(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        var utf8 = Encoding.UTF8.GetString(bytes);
        if (!utf8.Contains('\uFFFD') && LooksLikeReadableText(utf8))
            return utf8;

        var latin1 = Encoding.Latin1.GetString(bytes);
        Encoding winEnc;
        try { winEnc = Encoding.GetEncoding(1252); }
        catch (NotSupportedException) { winEnc = Encoding.Latin1; }
        var win1252 = winEnc.GetString(bytes);
        return ScoreDecodedText(latin1) >= ScoreDecodedText(win1252) ? latin1 : win1252;
    }

    private static bool LooksLikeReadableText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var sample = text.Length > 4000 ? text[..4000] : text;
        var bad = sample.Count(c => c == '\uFFFD' || char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
        return bad < sample.Length * 0.02;
    }

    private static int ScoreDecodedText(string text)
    {
        if (string.IsNullOrEmpty(text)) return int.MinValue;
        var sample = text.Length > 4000 ? text[..4000] : text;
        var score = 0;
        foreach (var c in sample)
        {
            if (c == '\uFFFD') score -= 50;
            else if ("áàâãéêíóôõúçÁÀÂÃÉÊÍÓÔÕÚÇ".IndexOf(c) >= 0) score += 3;
            else if (c is '<' or '>' or '"' or '\'') score += 1;
        }
        return score;
    }

    private static byte[] ToAnsiBytes(object? blob)
    {
        var text = BlobToUtf8(blob);
        return Encoding.Latin1.GetBytes(text);
    }

    private static string SanitizeFileName(string nome)
    {
        var safe = new string(nome.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "relatorio" : safe.Replace(' ', '_');
    }
}
