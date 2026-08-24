using System.Data.Common;
using System.Text.Json;
using Dapper;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;

namespace MdwConteudos.Api.Modules.Web;

public interface IPaineisWebService
{
    Task<object> ListAsync(string? search, string? tipo, string? ativo, int? clienteId, int? moduloId, int? pacoteId, int page, int perPage, CancellationToken ct);
    Task<object?> GetAsync(int id, CancellationToken ct);
    Task<object> CreateAsync(PainelUpsertForm form, byte[]? pbixBytes, string? pbixFileName, int? codUsuario, CancellationToken ct);
    Task UpdateAsync(int id, PainelUpsertForm form, byte[]? pbixBytes, string? pbixFileName, CancellationToken ct);
    Task SetStatusAsync(int id, object ativo, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<(byte[] Bytes, string FileName)> DownloadPbixAsync(int id, CancellationToken ct);
    Task<object> ListClientesAsync(CancellationToken ct);
    Task<object> ListModulosAsync(CancellationToken ct);
    Task<object> ListPacotesAsync(CancellationToken ct);
    Task<object> GetTipoCountsAsync(CancellationToken ct);
}

public class PaineisWebService : IPaineisWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public PaineisWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListAsync(
        string? search, string? tipo, string? ativo, int? clienteId, int? moduloId, int? pacoteId,
        int page, int perPage, CancellationToken ct)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        var offset = (page - 1) * perPage;

        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();
        p.Add("take", perPage);
        p.Add("skip", offset);

        var tipoNorm = NormalizeTipoFilter(tipo);
        if (tipoNorm is not null)
        {
            where.Add("p.TIPO_PAINEL = @tipo");
            p.Add("tipo", tipoNorm);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(UPPER(p.NOME) LIKE UPPER(@search) OR UPPER(p.DESCRICAO) LIKE UPPER(@search))");
            p.Add("search", $"%{search.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(ativo))
        {
            if (ativo is not ("0" or "1"))
                throw new InvalidOperationException("Parâmetro ativo inválido (use 0 ou 1)");
            where.Add("p.ATIVO = @ativo");
            p.Add("ativo", int.Parse(ativo));
        }
        if (clienteId.HasValue)
        {
            where.Add("p.CODCLIENTE = @clienteId");
            p.Add("clienteId", clienteId.Value);
        }
        if (moduloId.HasValue)
        {
            where.Add("p.CODMODULO = @moduloId");
            p.Add("moduloId", moduloId.Value);
        }

        var from = @"
            FROM Paineis p
            LEFT JOIN Clientes c ON p.CODCLIENTE = c.CODCLIENTE
            LEFT JOIN ModulosSistema m ON p.CODMODULO = m.CODMODULO";
        if (pacoteId.HasValue)
        {
            from += " INNER JOIN Paineis_Pacotes pp ON p.CODPAINEL = pp.CODPAINEL";
            where.Add("pp.CODPACOTECOMERCIAL = @pacoteId");
            p.Add("pacoteId", pacoteId.Value);
        }

        var whereClause = string.Join(" AND ", where);
        await using var conn = await _db.OpenConnectionAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(DISTINCT p.CODPAINEL) {from} WHERE {whereClause}", p);

        var rows = (await conn.QueryAsync($@"
            SELECT FIRST @take SKIP @skip DISTINCT
                   p.CODPAINEL, p.NOME, p.DESCRICAO, p.ATIVO, p.TIPO_PAINEL,
                   p.CODCLIENTE, c.NOME AS NOME_CLIENTE, p.CODMODULO, m.NOME AS NOME_MODULO,
                   CASE WHEN p.ARQUIVO_PBIX IS NOT NULL THEN 1 ELSE 0 END AS TEM_PBIX,
                   p.NOME_ARQUIVO_PBIX,
                   v.NUMEROVERSAO, v.DATACRIACAO, v.PUBLICADO
            {from}
            LEFT JOIN (
                SELECT v1.CODPAINEL, v1.NUMEROVERSAO,
                       COALESCE(v1.DATACRIACAO, CURRENT_TIMESTAMP) AS DATACRIACAO, v1.PUBLICADO
                FROM VersoesPainel v1
                INNER JOIN (
                    SELECT CODPAINEL, MAX(COALESCE(DATACRIACAO, CURRENT_TIMESTAMP)) AS MAX_DATA
                    FROM VersoesPainel
                    GROUP BY CODPAINEL
                ) v2 ON v1.CODPAINEL = v2.CODPAINEL
                     AND COALESCE(v1.DATACRIACAO, CURRENT_TIMESTAMP) = v2.MAX_DATA
            ) v ON p.CODPAINEL = v.CODPAINEL
            WHERE {whereClause}
            ORDER BY p.NOME", p)).ToList();

        var list = new List<object>();
        foreach (var r in rows)
        {
            var id = (int)r.CODPAINEL;
            var pacotes = (await conn.QueryAsync<string>(@"
                SELECT pc.NOME FROM Paineis_Pacotes pp
                JOIN PacotesComerciais pc ON pp.CODPACOTECOMERCIAL = pc.CODPACOTECOMERCIAL
                WHERE pp.CODPAINEL = @id ORDER BY pc.NOME", new { id })).ToList();

            list.Add(new
            {
                codpainel = id,
                nome = (string?)r.NOME,
                descricao = (string?)r.DESCRICAO,
                ativo = ToInt01(r.ATIVO),
                tipo_painel = (string?)r.TIPO_PAINEL,
                codcliente = r.CODCLIENTE is null ? null : (int?)Convert.ToInt32(r.CODCLIENTE),
                nome_cliente = (string?)r.NOME_CLIENTE,
                codmodulo = r.CODMODULO is null ? null : (int?)Convert.ToInt32(r.CODMODULO),
                nome_modulo = (string?)r.NOME_MODULO,
                tem_arquivo_pbix = ToInt01(r.TEM_PBIX) == 1,
                nome_arquivo_pbix = (string?)r.NOME_ARQUIVO_PBIX,
                ultima_versao = (string?)r.NUMEROVERSAO,
                data_versao = FormatDate(r.DATACRIACAO),
                publicado = r.PUBLICADO is null ? (int?)null : ToInt01(r.PUBLICADO),
                pacotes
            });
        }

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
        var r = await conn.QueryFirstOrDefaultAsync(@"
            SELECT p.CODPAINEL, p.CODCLIENTE, p.CODMODULO, p.NOME, p.DESCRICAO, p.ATIVO, p.TIPO_PAINEL,
                   c.NOME AS NOME_CLIENTE, m.NOME AS NOME_MODULO,
                   CASE WHEN p.ARQUIVO_PBIX IS NOT NULL THEN 1 ELSE 0 END AS TEM_PBIX,
                   p.NOME_ARQUIVO_PBIX
            FROM Paineis p
            LEFT JOIN Clientes c ON p.CODCLIENTE = c.CODCLIENTE
            LEFT JOIN ModulosSistema m ON p.CODMODULO = m.CODMODULO
            WHERE p.CODPAINEL = @id", new { id });
        if (r is null) return null;

        var pacotes = (await conn.QueryAsync(@"
            SELECT pc.CODPACOTECOMERCIAL AS Cod, pc.NOME AS Nome
            FROM Paineis_Pacotes pp
            JOIN PacotesComerciais pc ON pp.CODPACOTECOMERCIAL = pc.CODPACOTECOMERCIAL
            WHERE pp.CODPAINEL = @id ORDER BY pc.NOME", new { id }))
            .Select(x => new { codpacotecomercial = (int)x.Cod, nome = (string?)x.Nome })
            .ToList();

        var versoes = (await conn.QueryAsync(@"
            SELECT CODVERSAOPAINEL, NUMEROVERSAO,
                   COALESCE(DATACRIACAO, CURRENT_TIMESTAMP) AS DATACRIACAO, PUBLICADO, OBSERVACOES
            FROM VersoesPainel
            WHERE CODPAINEL = @id
            ORDER BY COALESCE(DATACRIACAO, CURRENT_TIMESTAMP) DESC", new { id }))
            .Select(v => new
            {
                codversaopainel = (int)v.CODVERSAOPAINEL,
                numeroversao = (string?)v.NUMEROVERSAO,
                datacriacao = FormatDate(v.DATACRIACAO),
                publicado = ToInt01(v.PUBLICADO),
                observacoes = (string?)v.OBSERVACOES
            }).ToList();

        return new
        {
            codpainel = (int)r.CODPAINEL,
            nome = (string?)r.NOME,
            descricao = (string?)r.DESCRICAO,
            ativo = ToInt01(r.ATIVO),
            tipo_painel = (string?)r.TIPO_PAINEL,
            codcliente = r.CODCLIENTE is null ? null : (int?)Convert.ToInt32(r.CODCLIENTE),
            nome_cliente = (string?)r.NOME_CLIENTE,
            codmodulo = Convert.ToInt32(r.CODMODULO),
            nome_modulo = (string?)r.NOME_MODULO,
            tem_arquivo_pbix = ToInt01(r.TEM_PBIX) == 1,
            nome_arquivo_pbix = (string?)r.NOME_ARQUIVO_PBIX,
            pacotes,
            versoes
        };
    }

    public async Task<object> CreateAsync(
        PainelUpsertForm form, byte[]? pbixBytes, string? pbixFileName, int? _codUsuario, CancellationToken ct)
    {
        var (nome, tipo, descricao, ativo, codCliente, codModulo, pacotes) = ValidateForm(form);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var cod = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Paineis (CODCLIENTE, CODMODULO, NOME, TIPO_PAINEL, DESCRICAO, ATIVO)
                VALUES (@codCliente, @codModulo, @nome, @tipo, @descricao, @ativo)
                RETURNING CODPAINEL",
                new { codCliente, codModulo, nome, tipo, descricao, ativo }, tx);

            if (tipo == "POWERBI" && pbixBytes is { Length: > 0 })
                await SavePbixAsync(conn, tx, cod, pbixFileName, pbixBytes);

            foreach (var pid in pacotes)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO Paineis_Pacotes (CODPAINEL, CODPACOTECOMERCIAL) VALUES (@cod, @pid)",
                    new { cod, pid }, tx);
            }

            var codVersao = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO VersoesPainel (CODPAINEL, NUMEROVERSAO, DATACRIACAO, PUBLICADO)
                VALUES (@cod, '1.0', CURRENT_TIMESTAMP, 0)
                RETURNING CODVERSAOPAINEL",
                new { cod }, tx);

            if (tipo == "POWERBI")
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO CONFIGURACOESPOWERBI
                    (CODVERSAOPAINEL, DIRETORIO_PBIX, NOME_ARQUIVO_PBIX, WORKSPACE_POWERBI,
                     DATASET_POWERBI, GATEWAY_POWERBI, FREQUENCIA_ATUALIZACAO, RESPONSAVEL_ATUALIZACAO, DATACRIACAO)
                    VALUES (@codVersao, @dir, @nomeArquivo, @workspace, @dataset, @gateway, @freq, @resp, CURRENT_TIMESTAMP)",
                    new
                    {
                        codVersao,
                        dir = form.DiretorioPbix ?? "",
                        nomeArquivo = form.NomeArquivoPbixMeta ?? "",
                        workspace = form.WorkspacePowerbi ?? "",
                        dataset = form.DatasetPowerbi ?? "",
                        gateway = form.GatewayPowerbi ?? "",
                        freq = form.FrequenciaAtualizacao ?? "",
                        resp = form.ResponsavelAtualizacao ?? ""
                    }, tx);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO CONFIGURACOESAPI (CODVERSAOPAINEL, RESPONSAVEL_API, DATACRIACAO)
                    VALUES (@codVersao, @resp, CURRENT_TIMESTAMP)",
                    new { codVersao, resp = form.ResponsavelApi ?? "" }, tx);
            }

            await tx.CommitAsync(ct);
            return new { success = true, message = "Painel criado com sucesso", codPainel = cod };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateAsync(int id, PainelUpsertForm form, byte[]? pbixBytes, string? pbixFileName, CancellationToken ct)
    {
        var (nome, tipo, descricao, ativo, codCliente, codModulo, pacotes) = ValidateForm(form);

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Paineis WHERE CODPAINEL = @id", new { id });
        if (exists == 0)
            throw new InvalidOperationException("Painel não encontrado");

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await conn.ExecuteAsync(@"
                UPDATE Paineis
                SET CODCLIENTE = @codCliente, CODMODULO = @codModulo, NOME = @nome,
                    TIPO_PAINEL = @tipo, DESCRICAO = @descricao, ATIVO = @ativo
                WHERE CODPAINEL = @id",
                new { codCliente, codModulo, nome, tipo, descricao, ativo, id }, tx);

            await conn.ExecuteAsync("DELETE FROM Paineis_Pacotes WHERE CODPAINEL = @id", new { id }, tx);
            foreach (var pid in pacotes)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO Paineis_Pacotes (CODPAINEL, CODPACOTECOMERCIAL) VALUES (@id, @pid)",
                    new { id, pid }, tx);
            }

            if (tipo == "POWERBI" && pbixBytes is { Length: > 0 })
                await SavePbixAsync(conn, tx, id, pbixFileName, pbixBytes);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task SetStatusAsync(int id, object ativoRaw, CancellationToken ct)
    {
        var ativo = ParseAtivo(ativoRaw, required: true);
        await using var conn = await _db.OpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(
            "UPDATE Paineis SET ATIVO = @ativo WHERE CODPAINEL = @id", new { ativo, id });
        if (n == 0) throw new InvalidOperationException("Painel não encontrado");
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Paineis WHERE CODPAINEL = @id", new { id });
        if (exists == 0) throw new InvalidOperationException("Painel não encontrado");

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var versoes = (await conn.QueryAsync<int>(
                "SELECT CODVERSAOPAINEL FROM VersoesPainel WHERE CODPAINEL = @id", new { id }, tx)).ToList();

            foreach (var vid in versoes)
            {
                await TryDeleteAsync(conn, tx, "DELETE FROM Metricas WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM Dimensoes WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM FontesDados WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM LOGALTERACOES WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM CONFIGURACOESPOWERBI WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM CONFIGURACOESAPI WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM ARQUIVOSVERSOES WHERE CODVERSAOPAINEL = @vid", new { vid });
                await TryDeleteAsync(conn, tx, "DELETE FROM IMAGENSPAINEL WHERE CODVERSAOPAINEL = @vid", new { vid });
            }

            await conn.ExecuteAsync("DELETE FROM VersoesPainel WHERE CODPAINEL = @id", new { id }, tx);
            await conn.ExecuteAsync("DELETE FROM Paineis_Pacotes WHERE CODPAINEL = @id", new { id }, tx);
            await conn.ExecuteAsync("DELETE FROM Paineis WHERE CODPAINEL = @id", new { id }, tx);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(byte[] Bytes, string FileName)> DownloadPbixAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT NOME, NOME_ARQUIVO_PBIX, ARQUIVO_PBIX FROM Paineis WHERE CODPAINEL = @id", new { id });
        if (row is null) throw new InvalidOperationException("Painel não encontrado");

        var bytes = BlobHelper.ToBytes((object?)row.ARQUIVO_PBIX);
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("Arquivo PBIX não disponível");

        return (bytes, SanitizePbixName((string?)row.NOME_ARQUIVO_PBIX ?? (string?)row.NOME, id));
    }

    public async Task<object> ListClientesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ClienteLookupRow>(@"
            SELECT CODCLIENTE AS CodCliente, NOME AS Nome
            FROM Clientes
            WHERE COALESCE(STATUS, 1) = 1
            ORDER BY NOME");
        return new { success = true, data = rows };
    }

    public async Task<object> ListModulosAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODMODULO AS CodModulo, NOME AS Nome
                FROM ModulosSistema WHERE ATIVO = 1 ORDER BY NOME");
            return new { success = true, data = rows };
        }
        catch
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODMODULO AS CodModulo, NOME AS Nome FROM ModulosSistema ORDER BY NOME");
            return new { success = true, data = rows };
        }
    }

    public async Task<object> ListPacotesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODPACOTECOMERCIAL AS CodPacote, NOME AS Nome
                FROM PacotesComerciais WHERE ATIVO = 1 ORDER BY NOME");
            return new { success = true, data = rows };
        }
        catch
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODPACOTECOMERCIAL AS CodPacote, NOME AS Nome FROM PacotesComerciais ORDER BY NOME");
            return new { success = true, data = rows };
        }
    }

    public async Task<object> GetTipoCountsAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var powerbi = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Paineis WHERE TIPO_PAINEL = 'POWERBI' AND ATIVO = 1");
        var api = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Paineis WHERE TIPO_PAINEL = 'API' AND ATIVO = 1");
        return new { success = true, powerbi, api };
    }

    private static async Task SavePbixAsync(
        DbConnection conn, DbTransaction tx, int id, string? fileName, byte[] bytes)
    {
        var nome = SanitizePbixName(fileName, id);
        await conn.ExecuteAsync(@"
            UPDATE Paineis SET NOME_ARQUIVO_PBIX = @nome, ARQUIVO_PBIX = @bytes
            WHERE CODPAINEL = @id",
            new { nome, bytes, id }, tx);
    }

    private static async Task TryDeleteAsync(DbConnection conn, DbTransaction tx, string sql, object param)
    {
        try { await conn.ExecuteAsync(sql, param, tx); }
        catch { /* table may not exist */ }
    }

    private static (string nome, string tipo, string descricao, int ativo, int? codCliente, int codModulo, List<int> pacotes)
        ValidateForm(PainelUpsertForm form)
    {
        var nome = (form.Nome ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Nome do painel é obrigatório");

        var tipo = (form.TipoPainel ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("POWERBI" or "API"))
            throw new InvalidOperationException("Tipo deve ser POWERBI ou API");

        if (form.CodModulo <= 0)
            throw new InvalidOperationException("Módulo é obrigatório");

        var ativo = form.Ativo is 0 or 1 ? form.Ativo : 1;
        var pacotes = (form.Pacotes ?? []).Distinct().Where(x => x > 0).ToList();
        return (nome, tipo, form.Descricao?.Trim() ?? "", ativo, form.CodCliente, form.CodModulo, pacotes);
    }

    private static string? NormalizeTipoFilter(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return null;
        var t = tipo.Trim().ToLowerInvariant();
        if (t is "todos" or "all") return null;
        if (t is "powerbi") return "POWERBI";
        if (t is "api") return "API";
        var u = tipo.Trim().ToUpperInvariant();
        if (u is "POWERBI" or "API") return u;
        throw new InvalidOperationException("Parâmetro tipo inválido (use powerbi, api ou todos)");
    }

    private static int ParseAtivo(object? valor, bool required, int defaultValue = 1)
    {
        if (valor is null)
        {
            if (required) throw new InvalidOperationException("Campo ativo é obrigatório");
            return defaultValue;
        }
        if (valor is bool b) return b ? 1 : 0;
        if (valor is int i && i is 0 or 1) return i;
        if (valor is long l && l is 0 or 1) return (int)l;
        if (valor is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n) && n is 0 or 1) return n;
            if (je.ValueKind == JsonValueKind.True) return 1;
            if (je.ValueKind == JsonValueKind.False) return 0;
            if (je.ValueKind == JsonValueKind.String) return ParseAtivo(je.GetString(), required, defaultValue);
        }
        var s = Convert.ToString(valor)?.Trim().ToUpperInvariant() ?? "";
        if (s is "1" or "S" or "TRUE" or "SIM") return 1;
        if (s is "0" or "N" or "FALSE" or "NAO" or "NÃO") return 0;
        throw new InvalidOperationException("Ativo inválido");
    }

    private static int ToInt01(object? value)
    {
        if (value is null or DBNull) return 0;
        try { return Convert.ToInt32(value) != 0 ? 1 : 0; }
        catch { return 0; }
    }

    private static string? FormatDate(object? value)
    {
        if (value is null or DBNull) return null;
        if (value is DateTime dt) return dt.ToString("dd/MM/yyyy HH:mm");
        return value.ToString();
    }

    private static string SanitizePbixName(string? nomeBase, int id)
    {
        var sane = string.Concat((nomeBase ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')).Trim().Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(sane)) sane = $"painel_{id}";
        if (!sane.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)) sane += ".pbix";
        return sane;
    }
}

internal sealed class ClienteLookupRow
{
    public int CodCliente { get; set; }
    public string Nome { get; set; } = "";
}
