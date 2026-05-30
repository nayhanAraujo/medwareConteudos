using System.Data;
using System.Security.Claims;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public sealed record UploadedFileContent(string FileName, byte[] Content);

public sealed record ReferenciaUpsertRequest(
    string Titulo,
    int Ano,
    string? Descricao,
    string? Doi,
    string? Isbn,
    string? Volume,
    string? Paginas,
    int? CodEspecialidade,
    int? CodTipoRef
);

public sealed record AnexoUpsertRequest(string Descricao, string Nome, string? Link);
public sealed record SaveAutoresRequest(List<int>? AutorIds);

public sealed record ReferenciaNormalidadeVinculoRequest(int CodNormalidade, int? Pagina);
public sealed record VincularNormalidadesRequest(int CodReferencia, List<ReferenciaNormalidadeVinculoRequest>? Normalidades);
public sealed record AtualizarNormalidadeReferenciaRequest(
    int CodNormalidade,
    double? ValorMin,
    double? ValorMax,
    int? IdadeMin,
    int? IdadeMax,
    int? Pagina
);
public sealed record DesvincularNormalidadeReferenciaRequest(int CodNormalidade, int? CodReferencia);
public sealed record ImportarNormalidadesRequest(int CodReferenciaDestino, int CodReferenciaOrigem);

public interface IReferenciasService
{
    Task<(IReadOnlyList<object> Items, int Page, int TotalPages, int TotalItems)> ListReferenciasAsync(
        int page,
        int pageSize,
        string? titulo,
        string? ano,
        string? autor,
        string? abreviacao,
        CancellationToken ct
    );

    Task<object?> GetReferenciaAsync(int codReferencia, CancellationToken ct);
    Task<int> CreateReferenciaAsync(ReferenciaUpsertRequest req, int codUsuario, CancellationToken ct);
    Task UpdateReferenciaAsync(int codReferencia, ReferenciaUpsertRequest req, int codUsuario, CancellationToken ct);
    Task DeleteReferenciaAsync(int codReferencia, CancellationToken ct);

    Task<IReadOnlyList<object>> ListEspecialidadesAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> ListTiposReferenciaAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> SearchReferenciasAsync(string? term, CancellationToken ct);

    Task<IReadOnlyList<object>> ListAnexosAsync(int codReferencia, CancellationToken ct);
    Task<IReadOnlyList<object>> ListAnexosCompatAsync(int codReferencia, CancellationToken ct);
    Task<int> CreateAnexoAsync(int codReferencia, AnexoUpsertRequest req, UploadedFileContent? file, int codUsuario, CancellationToken ct);
    Task<int> UpdateAnexoAsync(int codAnexo, AnexoUpsertRequest req, UploadedFileContent? file, int codUsuario, CancellationToken ct);
    Task<int> DeleteAnexoAsync(int codAnexo, CancellationToken ct);

    Task<IReadOnlyList<object>> ListAutoresAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> ListAutoresByReferenciaAsync(int codReferencia, CancellationToken ct);
    Task SaveAutoresByReferenciaAsync(int codReferencia, IEnumerable<int> autorIds, CancellationToken ct);

    Task<object> GetReferenciasNormalidadesAsync(int? referenciaId, string? referenciaBusca, string? variavelBusca, int limite, CancellationToken ct);
    Task<int> VincularNormalidadesAsync(int codReferencia, IEnumerable<ReferenciaNormalidadeVinculoRequest> normalidades, int codUsuario, CancellationToken ct);
    Task AtualizarNormalidadeReferenciaAsync(AtualizarNormalidadeReferenciaRequest req, int codUsuario, CancellationToken ct);
    Task DesvincularNormalidadeAsync(int codNormalidade, int codUsuario, CancellationToken ct);
    Task<object> ImportarNormalidadesAsync(int codReferenciaDestino, int codReferenciaOrigem, int codUsuario, CancellationToken ct);
}

public class ReferenciasService : IReferenciasService
{
    private readonly string _connectionString;
    private readonly string _repoRoot;
    private readonly string _staticUploadsRoot;

    public ReferenciasService(IConfiguration config)
    {
        _connectionString = EnvFileLoader.GetFirebirdConnectionString(config);
        var repo = config["LegacyPaths:RepoRoot"];
        if (string.IsNullOrWhiteSpace(repo))
            repo = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."));
        _repoRoot = repo;
        _staticUploadsRoot = Path.Combine(_repoRoot, "mdw-migracao", "static", "uploads");
        Directory.CreateDirectory(_staticUploadsRoot);
    }

    private IDbConnection CreateConnection() => new FbConnection(_connectionString);

    public async Task<(IReadOnlyList<object> Items, int Page, int TotalPages, int TotalItems)> ListReferenciasAsync(
        int page,
        int pageSize,
        string? titulo,
        string? ano,
        string? autor,
        string? abreviacao,
        CancellationToken ct
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(titulo))
        {
            where.Add("UPPER(r.TITULO) LIKE UPPER(@titulo)");
            p.Add("titulo", $"%{titulo.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(ano))
        {
            where.Add("CAST(r.ANO AS VARCHAR(10)) = @ano");
            p.Add("ano", ano.Trim());
        }
        if (!string.IsNullOrWhiteSpace(autor))
        {
            where.Add("UPPER(a.NOME) LIKE UPPER(@autor)");
            p.Add("autor", $"%{autor.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(abreviacao))
        {
            where.Add("UPPER(a.ABREVIACAO) LIKE UPPER(@abreviacao)");
            p.Add("abreviacao", $"%{abreviacao.Trim()}%");
        }

        var whereSql = string.Join(" AND ", where);

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(
            $@"
            SELECT COUNT(DISTINCT r.CODREFERENCIA)
            FROM REFERENCIA r
            LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
            LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
            WHERE {whereSql}",
            p
        );

        var totalPages = total > 0 ? (int)Math.Ceiling(total / (double)pageSize) : 0;

        p.Add("first", pageSize);
        p.Add("skip", skip);
        var rows = await conn.QueryAsync(
            $@"
            SELECT FIRST @first SKIP @skip
                r.CODREFERENCIA,
                r.TITULO,
                r.ANO,
                LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS AUTORES,
                r.DESCRICAO,
                e.NOME AS ESPECIALIDADE,
                (SELECT COUNT(DISTINCT n.CODVARIAVEL) FROM NORMALIDADE n WHERE n.CODREFERENCIA = r.CODREFERENCIA) AS TOTAL_VARIAVEIS,
                (SELECT COUNT(*) FROM NORMALIDADE n WHERE n.CODREFERENCIA = r.CODREFERENCIA) AS TOTAL_NORMALIDADES
            FROM REFERENCIA r
            LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
            LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
            LEFT JOIN ESPECIALIDADE e ON r.CODESPECIALIDADE = e.CODESPECIALIDADE
            WHERE {whereSql}
            GROUP BY r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO, e.NOME
            ORDER BY r.ANO DESC NULLS LAST, r.TITULO",
            p
        );

        var items = rows.Select(row =>
        {
            var d = AsDict(row);
            return (object)new
            {
                codReferencia = ToInt(d, "CODREFERENCIA"),
                titulo = ToStr(d, "TITULO") ?? "",
                ano = ToNullableInt(d, "ANO"),
                autores = ToStr(d, "AUTORES"),
                descricao = ToStr(d, "DESCRICAO"),
                especialidade = ToStr(d, "ESPECIALIDADE"),
                totalVariaveis = ToNullableInt(d, "TOTAL_VARIAVEIS") ?? 0,
                totalNormalidades = ToNullableInt(d, "TOTAL_NORMALIDADES") ?? 0
            };
        }).ToList();

        return (items, page, totalPages, total);
    }

    public async Task<object?> GetReferenciaAsync(int codReferencia, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var row = await conn.QueryFirstOrDefaultAsync(
            @"
            SELECT
                r.CODREFERENCIA,
                r.TITULO,
                r.ANO,
                r.DESCRICAO,
                r.DOI,
                r.ISBN,
                r.VOLUME,
                r.PAGINAS,
                r.CODESPECIALIDADE,
                r.CODTIPOREF,
                e.NOME AS ESPECIALIDADE,
                tr.DESCRICAO AS TIPO_REFERENCIA,
                COALESCE((
                    SELECT LIST(a.NOME || COALESCE(' (' || a.ABREVIACAO || ')', ''), ', ')
                    FROM REFERENCIA_AUTORES ra
                    JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
                    WHERE ra.CODREFERENCIA = r.CODREFERENCIA
                ), '') AS AUTORES
            FROM REFERENCIA r
            LEFT JOIN ESPECIALIDADE e ON r.CODESPECIALIDADE = e.CODESPECIALIDADE
            LEFT JOIN TIPO_REFERENCIA tr ON r.CODTIPOREF = tr.CODTIPOREF
            WHERE r.CODREFERENCIA = @cod",
            new { cod = codReferencia }
        );

        if (row is null) return null;
        var d = AsDict(row);
        return new
        {
            codReferencia = ToInt(d, "CODREFERENCIA"),
            titulo = ToStr(d, "TITULO") ?? "",
            ano = ToNullableInt(d, "ANO"),
            descricao = ToStr(d, "DESCRICAO"),
            doi = ToStr(d, "DOI"),
            isbn = ToStr(d, "ISBN"),
            volume = ToStr(d, "VOLUME"),
            paginas = ToStr(d, "PAGINAS"),
            codEspecialidade = ToNullableInt(d, "CODESPECIALIDADE"),
            codTipoRef = ToNullableInt(d, "CODTIPOREF"),
            especialidade = ToStr(d, "ESPECIALIDADE"),
            tipoReferencia = ToStr(d, "TIPO_REFERENCIA"),
            autores = ToStr(d, "AUTORES")
        };
    }

    public async Task<int> CreateReferenciaAsync(ReferenciaUpsertRequest req, int codUsuario, CancellationToken ct)
    {
        ValidateReferencia(req);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var cod = await conn.ExecuteScalarAsync<int>(
            @"
            INSERT INTO REFERENCIA (
                TITULO, ANO, DESCRICAO, DOI, ISBN, VOLUME, PAGINAS,
                CODESPECIALIDADE, CODTIPOREF, CODUSUARIO, DTHRULTMODIFICACAO
            )
            VALUES (
                @Titulo, @Ano, @Descricao, @Doi, @Isbn, @Volume, @Paginas,
                @CodEspecialidade, @CodTipoRef, @CodUsuario, @Now
            )
            RETURNING CODREFERENCIA",
            new
            {
                req.Titulo,
                req.Ano,
                req.Descricao,
                req.Doi,
                req.Isbn,
                req.Volume,
                req.Paginas,
                req.CodEspecialidade,
                req.CodTipoRef,
                CodUsuario = codUsuario,
                Now = DateTime.Now
            }
        );

        return cod;
    }

    public async Task UpdateReferenciaAsync(int codReferencia, ReferenciaUpsertRequest req, int codUsuario, CancellationToken ct)
    {
        ValidateReferencia(req);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var affected = await conn.ExecuteAsync(
            @"
            UPDATE REFERENCIA SET
                TITULO = @Titulo,
                ANO = @Ano,
                DESCRICAO = @Descricao,
                DOI = @Doi,
                ISBN = @Isbn,
                VOLUME = @Volume,
                PAGINAS = @Paginas,
                CODESPECIALIDADE = @CodEspecialidade,
                CODTIPOREF = @CodTipoRef,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Now
            WHERE CODREFERENCIA = @CodReferencia",
            new
            {
                req.Titulo,
                req.Ano,
                req.Descricao,
                req.Doi,
                req.Isbn,
                req.Volume,
                req.Paginas,
                req.CodEspecialidade,
                req.CodTipoRef,
                CodUsuario = codUsuario,
                Now = DateTime.Now,
                CodReferencia = codReferencia
            }
        );
        if (affected == 0) throw new InvalidOperationException("Referência não encontrada.");
    }

    public async Task DeleteReferenciaAsync(int codReferencia, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var anexos = await conn.QueryAsync(
                "SELECT CAMINHO, LINK FROM ANEXOS WHERE CODREFERENCIA = @cod",
                new { cod = codReferencia }, tx
            );
            foreach (var anexo in anexos)
            {
                var d = AsDict(anexo);
                DeletePhysicalFile(ToStr(d, "CAMINHO"), ToStr(d, "LINK"));
            }

            await conn.ExecuteAsync("DELETE FROM ANEXOS WHERE CODREFERENCIA = @cod", new { cod = codReferencia }, tx);
            await conn.ExecuteAsync("DELETE FROM REFERENCIA_AUTORES WHERE CODREFERENCIA = @cod", new { cod = codReferencia }, tx);
            var deleted = await conn.ExecuteAsync("DELETE FROM REFERENCIA WHERE CODREFERENCIA = @cod", new { cod = codReferencia }, tx);
            if (deleted == 0) throw new InvalidOperationException("Referência não encontrada.");
            await tx.CommitAsync(ct);
        }
        catch (FbException)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException("Erro ao excluir: referência está vinculada a outras tabelas.");
        }
    }

    public async Task<IReadOnlyList<object>> ListEspecialidadesAsync(CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync("SELECT CODESPECIALIDADE, NOME FROM ESPECIALIDADE ORDER BY NOME");
        return rows.Select(r =>
        {
            var d = AsDict(r);
            return (object)new { codEspecialidade = ToInt(d, "CODESPECIALIDADE"), nome = ToStr(d, "NOME") ?? "" };
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> ListTiposReferenciaAsync(CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync("SELECT CODTIPOREF, DESCRICAO FROM TIPO_REFERENCIA ORDER BY DESCRICAO");
        return rows.Select(r =>
        {
            var d = AsDict(r);
            return (object)new { codTipoRef = ToInt(d, "CODTIPOREF"), descricao = ToStr(d, "DESCRICAO") ?? "" };
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> SearchReferenciasAsync(string? term, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync(
            @"
            SELECT FIRST 20 r.CODREFERENCIA, r.TITULO, r.ANO,
                   LIST(a.NOME, ', ') AS AUTORES
            FROM REFERENCIA r
            LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
            LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
            WHERE UPPER(r.TITULO) LIKE UPPER(@term)
            GROUP BY r.CODREFERENCIA, r.TITULO, r.ANO
            ORDER BY r.ANO DESC",
            new { term = $"%{(term ?? string.Empty).Trim()}%" }
        );

        return rows.Select(r =>
        {
            var d = AsDict(r);
            var titulo = ToStr(d, "TITULO") ?? "";
            var ano = ToNullableInt(d, "ANO");
            var autores = ToStr(d, "AUTORES");
            return (object)new
            {
                id = ToInt(d, "CODREFERENCIA"),
                text = $"{titulo} ({(ano?.ToString() ?? "s/ano")})" + (string.IsNullOrWhiteSpace(autores) ? "" : $" - {autores}"),
                titulo,
                ano,
                autores
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> ListAnexosAsync(int codReferencia, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync(
            @"
            SELECT CODANEXO, CODREFERENCIA, DESCRICAO, NOME, LINK, CAMINHO, TIPO_ANEXO
            FROM ANEXOS
            WHERE CODREFERENCIA = @cod
            ORDER BY CODANEXO DESC",
            new { cod = codReferencia }
        );

        return rows.Select(r =>
        {
            var d = AsDict(r);
            var link = NormalizeAnexoUrl(ToStr(d, "LINK"), ToStr(d, "CAMINHO"), ToStr(d, "TIPO_ANEXO"));
            return (object)new
            {
                codAnexo = ToInt(d, "CODANEXO"),
                codReferencia = ToInt(d, "CODREFERENCIA"),
                descricao = ToStr(d, "DESCRICAO"),
                nome = ToStr(d, "NOME"),
                link,
                caminho = ToStr(d, "CAMINHO"),
                tipoAnexo = ToStr(d, "TIPO_ANEXO")
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> ListAnexosCompatAsync(int codReferencia, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync(
            @"
            SELECT TIPO_ANEXO, LINK, CAMINHO, DESCRICAO
            FROM ANEXOS
            WHERE CODREFERENCIA = @cod",
            new { cod = codReferencia }
        );

        return rows.Select(r =>
        {
            var d = AsDict(r);
            var tipo = ToStr(d, "TIPO_ANEXO") ?? "TEXTO";
            return (object)new
            {
                tipo,
                caminho = NormalizeAnexoUrl(ToStr(d, "LINK"), ToStr(d, "CAMINHO"), tipo),
                descricao = ToStr(d, "DESCRICAO") ?? tipo
            };
        }).ToList();
    }

    public async Task<int> CreateAnexoAsync(int codReferencia, AnexoUpsertRequest req, UploadedFileContent? file, int codUsuario, CancellationToken ct)
    {
        ValidateAnexo(req);
        var (tipoAnexo, link, caminho) = await ResolveAnexoFileAndTypeAsync(req.Link, file, null, null);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var cod = await conn.ExecuteScalarAsync<int>(
            @"
            INSERT INTO ANEXOS (CODREFERENCIA, DESCRICAO, NOME, LINK, CAMINHO, TIPO_ANEXO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@CodReferencia, @Descricao, @Nome, @Link, @Caminho, @TipoAnexo, @CodUsuario, @Now)
            RETURNING CODANEXO",
            new
            {
                CodReferencia = codReferencia,
                req.Descricao,
                req.Nome,
                Link = link,
                Caminho = caminho,
                TipoAnexo = tipoAnexo,
                CodUsuario = codUsuario,
                Now = DateTime.Now
            }
        );
        return cod;
    }

    public async Task<int> UpdateAnexoAsync(int codAnexo, AnexoUpsertRequest req, UploadedFileContent? file, int codUsuario, CancellationToken ct)
    {
        ValidateAnexo(req);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var current = await conn.QueryFirstOrDefaultAsync(
            "SELECT CODREFERENCIA, CAMINHO, LINK FROM ANEXOS WHERE CODANEXO = @cod",
            new { cod = codAnexo }
        );
        if (current is null) throw new InvalidOperationException("Anexo não encontrado.");
        var dc = AsDict(current);
        var codReferencia = ToInt(dc, "CODREFERENCIA");
        var oldCaminho = ToStr(dc, "CAMINHO");
        var oldLink = ToStr(dc, "LINK");

        var resolved = await ResolveAnexoFileAndTypeAsync(req.Link, file, oldCaminho, oldLink);
        var tipoAnexo = resolved.tipoAnexo;
        var link = resolved.link;
        var caminho = resolved.caminho;
        var affected = await conn.ExecuteAsync(
            @"
            UPDATE ANEXOS SET
                DESCRICAO = @Descricao,
                NOME = @Nome,
                LINK = @Link,
                CAMINHO = @Caminho,
                TIPO_ANEXO = @TipoAnexo,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Now
            WHERE CODANEXO = @CodAnexo",
            new
            {
                req.Descricao,
                req.Nome,
                Link = link,
                Caminho = caminho,
                TipoAnexo = tipoAnexo,
                CodUsuario = codUsuario,
                Now = DateTime.Now,
                CodAnexo = codAnexo
            }
        );
        if (affected == 0) throw new InvalidOperationException("Anexo não encontrado.");
        return codReferencia;
    }

    public async Task<int> DeleteAnexoAsync(int codAnexo, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT CODREFERENCIA, CAMINHO, LINK FROM ANEXOS WHERE CODANEXO = @cod",
            new { cod = codAnexo }
        );
        if (row is null) throw new InvalidOperationException("Anexo não encontrado.");
        var d = AsDict(row);
        var codReferencia = ToInt(d, "CODREFERENCIA");
        DeletePhysicalFile(ToStr(d, "CAMINHO"), ToStr(d, "LINK"));
        await conn.ExecuteAsync("DELETE FROM ANEXOS WHERE CODANEXO = @cod", new { cod = codAnexo });
        return codReferencia;
    }

    public async Task<IReadOnlyList<object>> ListAutoresAsync(CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync("SELECT CODAUTOR, NOME, ABREVIACAO FROM AUTORES ORDER BY NOME");
        return rows.Select(r =>
        {
            var d = AsDict(r);
            return (object)new
            {
                codAutor = ToInt(d, "CODAUTOR"),
                nome = ToStr(d, "NOME") ?? "",
                abreviacao = ToStr(d, "ABREVIACAO")
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> ListAutoresByReferenciaAsync(int codReferencia, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var associados = (await conn.QueryAsync<int>(
            "SELECT CODAUTOR FROM REFERENCIA_AUTORES WHERE CODREFERENCIA = @cod",
            new { cod = codReferencia }
        )).ToHashSet();

        var autores = await ListAutoresAsync(ct);
        return autores.Select(a =>
        {
            var d = AsDict(a);
            var codAutor = ToInt(d, "codAutor");
            return (object)new
            {
                codAutor,
                nome = ToStr(d, "nome") ?? "",
                abreviacao = ToStr(d, "abreviacao"),
                selecionado = associados.Contains(codAutor)
            };
        }).ToList();
    }

    public async Task SaveAutoresByReferenciaAsync(int codReferencia, IEnumerable<int> autorIds, CancellationToken ct)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync("DELETE FROM REFERENCIA_AUTORES WHERE CODREFERENCIA = @cod", new { cod = codReferencia }, tx);
        foreach (var codAutor in autorIds.Distinct())
        {
            await conn.ExecuteAsync(
                "INSERT INTO REFERENCIA_AUTORES (CODREFERENCIA, CODAUTOR) VALUES (@codReferencia, @codAutor)",
                new { codReferencia, codAutor },
                tx
            );
        }
        await tx.CommitAsync(ct);
    }

    public async Task<object> GetReferenciasNormalidadesAsync(
        int? referenciaId,
        string? referenciaBusca,
        string? variavelBusca,
        int limite,
        CancellationToken ct
    )
    {
        limite = Math.Clamp(limite, 10, 200);
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);

        var where = new List<string> { "1=1" };
        var pRefs = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(referenciaBusca))
        {
            where.Add("(UPPER(r.TITULO) LIKE UPPER(@busca) OR CAST(r.CODREFERENCIA AS VARCHAR(20)) LIKE @busca OR UPPER(COALESCE(r.DESCRICAO, '')) LIKE UPPER(@busca))");
            pRefs.Add("busca", $"%{referenciaBusca.Trim()}%");
        }

        var refsRows = await conn.QueryAsync(
            $@"
            SELECT
                r.CODREFERENCIA,
                r.TITULO,
                r.ANO,
                COALESCE((
                    SELECT LIST(a.NOME, ', ')
                    FROM REFERENCIA_AUTORES ra
                    JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
                    WHERE ra.CODREFERENCIA = r.CODREFERENCIA
                ), '') AS AUTORES,
                (SELECT COUNT(DISTINCT n.CODVARIAVEL) FROM NORMALIDADE n WHERE n.CODREFERENCIA = r.CODREFERENCIA) AS TOTAL_VARIAVEIS,
                (SELECT COUNT(*) FROM NORMALIDADE n WHERE n.CODREFERENCIA = r.CODREFERENCIA) AS TOTAL_NORMALIDADES
            FROM REFERENCIA r
            WHERE {string.Join(" AND ", where)}
            ORDER BY r.ANO DESC NULLS LAST, r.TITULO",
            pRefs
        );

        var referencias = refsRows.Select(r =>
        {
            var d = AsDict(r);
            return new
            {
                codigo = ToInt(d, "CODREFERENCIA"),
                titulo = ToStr(d, "TITULO") ?? "",
                ano = ToNullableInt(d, "ANO"),
                autores = ToStr(d, "AUTORES"),
                totalVariaveis = ToNullableInt(d, "TOTAL_VARIAVEIS") ?? 0,
                totalNormalidades = ToNullableInt(d, "TOTAL_NORMALIDADES") ?? 0
            };
        }).ToList();

        int? refSelecionadaId = referenciaId;
        if (!refSelecionadaId.HasValue)
        {
            var firstRef = referencias.FirstOrDefault();
            if (firstRef != null)
                refSelecionadaId = firstRef.codigo;
        }
        object? referenciaSelecionada = null;
        IReadOnlyList<object> variaveisVinculadas = [];
        Dictionary<int, List<object>> normalidadesPorVariavel = new();
        IReadOnlyList<object> anexosReferencia = [];
        IReadOnlyList<object> normalidadesDisponiveis = [];
        var totalSemReferencia = 0;

        if (refSelecionadaId.HasValue)
        {
            var refRow = await conn.QueryFirstOrDefaultAsync(
                @"
                SELECT r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO,
                       COALESCE((
                           SELECT LIST(a.NOME || COALESCE(' (' || a.ABREVIACAO || ')', ''), ', ')
                           FROM REFERENCIA_AUTORES ra
                           JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
                           WHERE ra.CODREFERENCIA = r.CODREFERENCIA
                       ), '') AS AUTORES
                FROM REFERENCIA r
                WHERE r.CODREFERENCIA = @cod",
                new { cod = refSelecionadaId.Value }
            );

            if (refRow != null)
            {
                var dRef = AsDict(refRow);
                referenciaSelecionada = new
                {
                    codigo = ToInt(dRef, "CODREFERENCIA"),
                    titulo = ToStr(dRef, "TITULO") ?? "",
                    ano = ToNullableInt(dRef, "ANO"),
                    descricao = ToStr(dRef, "DESCRICAO"),
                    autores = ToStr(dRef, "AUTORES")
                };

                var varsRows = await conn.QueryAsync(
                    @"
                    SELECT v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA, COUNT(n.CODNORMALIDADE) AS TOTAL_NORMALIDADES
                    FROM VARIAVEIS v
                    JOIN NORMALIDADE n ON v.CODVARIAVEL = n.CODVARIAVEL
                    WHERE n.CODREFERENCIA = @cod
                    GROUP BY v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA
                    ORDER BY v.NOME",
                    new { cod = refSelecionadaId.Value }
                );
                variaveisVinculadas = varsRows.Select(r =>
                {
                    var d = AsDict(r);
                    return (object)new
                    {
                        codVariavel = ToInt(d, "CODVARIAVEL"),
                        nomeVariavel = ToStr(d, "NOME") ?? "",
                        variavel = ToStr(d, "VARIAVEL"),
                        sigla = ToStr(d, "SIGLA"),
                        totalNormalidades = ToNullableInt(d, "TOTAL_NORMALIDADES") ?? 0
                    };
                }).ToList();

                var normsRows = await conn.QueryAsync(
                    @"
                    SELECT n.CODNORMALIDADE, v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA,
                           n.SEXO, n.VALORMIN, n.VALORMAX,
                           n.IDADE_MIN, n.IDADE_MAX, n.PAGINA_REFERENCIA,
                           c.NOME AS CLASSIFICACAO
                    FROM NORMALIDADE n
                    JOIN VARIAVEIS v ON n.CODVARIAVEL = v.CODVARIAVEL
                    LEFT JOIN CLASSIFICACOES c ON n.CODCLASSIFICACAO = c.CODCLASSIFICACAO
                    WHERE n.CODREFERENCIA = @cod
                    ORDER BY v.NOME, n.SEXO",
                    new { cod = refSelecionadaId.Value }
                );

                foreach (var row in normsRows)
                {
                    var d = AsDict(row);
                    var codVariavel = ToInt(d, "CODVARIAVEL");
                    if (!normalidadesPorVariavel.TryGetValue(codVariavel, out List<object>? list))
                    {
                        list = [];
                        normalidadesPorVariavel[codVariavel] = list;
                    }
                    list.Add(new
                    {
                        codNormalidade = ToInt(d, "CODNORMALIDADE"),
                        codVariavel,
                        nomeVariavel = ToStr(d, "NOME") ?? "",
                        variavel = ToStr(d, "VARIAVEL"),
                        sigla = ToStr(d, "SIGLA"),
                        sexo = ToStr(d, "SEXO"),
                        valorMin = ToNullableDouble(d, "VALORMIN"),
                        valorMax = ToNullableDouble(d, "VALORMAX"),
                        idadeMin = ToNullableInt(d, "IDADE_MIN"),
                        idadeMax = ToNullableInt(d, "IDADE_MAX"),
                        pagina = ToNullableInt(d, "PAGINA_REFERENCIA"),
                        classificacao = ToStr(d, "CLASSIFICACAO")
                    });
                }

                anexosReferencia = await ListAnexosCompatAsync(refSelecionadaId.Value, ct);

                var whereDisp = new List<string> { "n.CODREFERENCIA IS NULL" };
                var pDisp = new DynamicParameters();
                if (!string.IsNullOrWhiteSpace(variavelBusca))
                {
                    whereDisp.Add("(UPPER(v.NOME) LIKE UPPER(@bv) OR UPPER(v.VARIAVEL) LIKE UPPER(@bv) OR UPPER(v.SIGLA) LIKE UPPER(@bv))");
                    pDisp.Add("bv", $"%{variavelBusca.Trim()}%");
                }

                var dispRows = await conn.QueryAsync(
                    $@"
                    SELECT FIRST {limite}
                           n.CODNORMALIDADE, v.CODVARIAVEL, v.NOME, v.VARIAVEL, v.SIGLA,
                           n.SEXO, n.VALORMIN, n.VALORMAX, n.IDADE_MIN, n.IDADE_MAX,
                           c.NOME AS CLASSIFICACAO
                    FROM NORMALIDADE n
                    JOIN VARIAVEIS v ON n.CODVARIAVEL = v.CODVARIAVEL
                    LEFT JOIN CLASSIFICACOES c ON n.CODCLASSIFICACAO = c.CODCLASSIFICACAO
                    WHERE {string.Join(" AND ", whereDisp)}
                    ORDER BY v.NOME, n.SEXO",
                    pDisp
                );
                normalidadesDisponiveis = dispRows.Select(r =>
                {
                    var d = AsDict(r);
                    return (object)new
                    {
                        codNormalidade = ToInt(d, "CODNORMALIDADE"),
                        codVariavel = ToInt(d, "CODVARIAVEL"),
                        nomeVariavel = ToStr(d, "NOME") ?? "",
                        variavel = ToStr(d, "VARIAVEL"),
                        sigla = ToStr(d, "SIGLA"),
                        sexo = ToStr(d, "SEXO"),
                        valorMin = ToNullableDouble(d, "VALORMIN"),
                        valorMax = ToNullableDouble(d, "VALORMAX"),
                        idadeMin = ToNullableInt(d, "IDADE_MIN"),
                        idadeMax = ToNullableInt(d, "IDADE_MAX"),
                        classificacao = ToStr(d, "CLASSIFICACAO")
                    };
                }).ToList();

                totalSemReferencia = await conn.ExecuteScalarAsync<int>(
                    $@"
                    SELECT COUNT(*)
                    FROM NORMALIDADE n
                    JOIN VARIAVEIS v ON n.CODVARIAVEL = v.CODVARIAVEL
                    WHERE {string.Join(" AND ", whereDisp)}",
                    pDisp
                );
            }
        }

        var outrasReferencias = referencias.Where(r => r.codigo != refSelecionadaId).ToList();

        return new
        {
            referencias,
            outrasReferencias,
            referenciaSelecionada,
            variaveisVinculadas,
            normalidadesPorVariavel = normalidadesPorVariavel.ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value
            ),
            normalidadesDisponiveis,
            anexosReferencia,
            totalSemReferencia,
            limiteDisponiveis = limite,
            filtros = new { referenciaBusca = referenciaBusca ?? "", variavelBusca = variavelBusca ?? "" }
        };
    }

    public async Task<int> VincularNormalidadesAsync(
        int codReferencia,
        IEnumerable<ReferenciaNormalidadeVinculoRequest> normalidades,
        int codUsuario,
        CancellationToken ct
    )
    {
        var itens = normalidades?.Where(n => n.CodNormalidade > 0).ToList() ?? [];
        if (!itens.Any())
            throw new InvalidOperationException("Selecione ao menos uma normalidade para vincular.");

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var n in itens)
        {
            await conn.ExecuteAsync(
                @"
                UPDATE NORMALIDADE
                SET CODREFERENCIA = @CodReferencia,
                    PAGINA_REFERENCIA = @Pagina,
                    CODUSUARIO = @CodUsuario,
                    DTHRULTMODIFICACAO = @Now
                WHERE CODNORMALIDADE = @CodNormalidade",
                new
                {
                    CodReferencia = codReferencia,
                    Pagina = n.Pagina,
                    CodUsuario = codUsuario,
                    Now = DateTime.Now,
                    n.CodNormalidade
                },
                tx
            );
        }
        await tx.CommitAsync(ct);
        return itens.Count;
    }

    public async Task AtualizarNormalidadeReferenciaAsync(AtualizarNormalidadeReferenciaRequest req, int codUsuario, CancellationToken ct)
    {
        if (req.CodNormalidade <= 0)
            throw new InvalidOperationException("Código da normalidade não informado.");
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            @"
            UPDATE NORMALIDADE
            SET VALORMIN = @ValorMin,
                VALORMAX = @ValorMax,
                IDADE_MIN = @IdadeMin,
                IDADE_MAX = @IdadeMax,
                PAGINA_REFERENCIA = @Pagina,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Now
            WHERE CODNORMALIDADE = @CodNormalidade",
            new
            {
                req.ValorMin,
                req.ValorMax,
                req.IdadeMin,
                req.IdadeMax,
                req.Pagina,
                CodUsuario = codUsuario,
                Now = DateTime.Now,
                req.CodNormalidade
            }
        );
    }

    public async Task DesvincularNormalidadeAsync(int codNormalidade, int codUsuario, CancellationToken ct)
    {
        if (codNormalidade <= 0)
            throw new InvalidOperationException("Normalidade inválida.");
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            @"
            UPDATE NORMALIDADE
            SET CODREFERENCIA = NULL,
                PAGINA_REFERENCIA = NULL,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Now
            WHERE CODNORMALIDADE = @CodNormalidade",
            new { CodUsuario = codUsuario, Now = DateTime.Now, CodNormalidade = codNormalidade }
        );
    }

    public async Task<object> ImportarNormalidadesAsync(int codReferenciaDestino, int codReferenciaOrigem, int codUsuario, CancellationToken ct)
    {
        if (codReferenciaDestino <= 0 || codReferenciaOrigem <= 0)
            throw new InvalidOperationException("Informe referências de origem e destino válidas.");
        if (codReferenciaDestino == codReferenciaOrigem)
            throw new InvalidOperationException("Origem e destino não podem ser a mesma referência.");

        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var existeDestino = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM REFERENCIA WHERE CODREFERENCIA = @cod",
            new { cod = codReferenciaDestino }, tx
        );
        if (existeDestino == 0) throw new InvalidOperationException("Referência de destino não encontrada.");
        var existeOrigem = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM REFERENCIA WHERE CODREFERENCIA = @cod",
            new { cod = codReferenciaOrigem }, tx
        );
        if (existeOrigem == 0) throw new InvalidOperationException("Referência de origem não encontrada.");

        var origemRows = await conn.QueryAsync(
            @"
            SELECT CODVARIAVEL, CODCLASSIFICACAO, SEXO, VALORMIN, VALORMAX, IDADE_MIN, IDADE_MAX, PAGINA_REFERENCIA
            FROM NORMALIDADE
            WHERE CODREFERENCIA = @cod",
            new { cod = codReferenciaOrigem }, tx
        );

        var inseridas = 0;
        var ignoradas = 0;
        foreach (var row in origemRows)
        {
            var d = AsDict(row);
            var codVariavel = ToInt(d, "CODVARIAVEL");
            var codClassificacao = ToNullableInt(d, "CODCLASSIFICACAO");
            var sexo = ToStr(d, "SEXO");
            var valorMin = ToNullableDouble(d, "VALORMIN");
            var valorMax = ToNullableDouble(d, "VALORMAX");
            var idadeMin = ToNullableInt(d, "IDADE_MIN");
            var idadeMax = ToNullableInt(d, "IDADE_MAX");
            var pagina = ToNullableInt(d, "PAGINA_REFERENCIA");

            var exists = await conn.QueryFirstOrDefaultAsync<int?>(
                @"
                SELECT 1 FROM NORMALIDADE
                WHERE CODREFERENCIA = @CodReferenciaDestino
                  AND CODVARIAVEL = @CodVariavel
                  AND SEXO IS NOT DISTINCT FROM @Sexo
                  AND VALORMIN IS NOT DISTINCT FROM @ValorMin
                  AND VALORMAX IS NOT DISTINCT FROM @ValorMax
                  AND IDADE_MIN IS NOT DISTINCT FROM @IdadeMin
                  AND IDADE_MAX IS NOT DISTINCT FROM @IdadeMax
                  AND CODCLASSIFICACAO IS NOT DISTINCT FROM @CodClassificacao",
                new
                {
                    CodReferenciaDestino = codReferenciaDestino,
                    CodVariavel = codVariavel,
                    Sexo = sexo,
                    ValorMin = valorMin,
                    ValorMax = valorMax,
                    IdadeMin = idadeMin,
                    IdadeMax = idadeMax,
                    CodClassificacao = codClassificacao
                },
                tx
            );
            if (exists.HasValue)
            {
                ignoradas++;
                continue;
            }

            await conn.ExecuteAsync(
                @"
                INSERT INTO NORMALIDADE (
                    CODVARIAVEL, CODREFERENCIA, CODCLASSIFICACAO, SEXO,
                    VALORMIN, VALORMAX, IDADE_MIN, IDADE_MAX, PAGINA_REFERENCIA,
                    CODUSUARIO, DTHRULTMODIFICACAO
                ) VALUES (
                    @CodVariavel, @CodReferenciaDestino, @CodClassificacao, @Sexo,
                    @ValorMin, @ValorMax, @IdadeMin, @IdadeMax, @Pagina,
                    @CodUsuario, @Now
                )",
                new
                {
                    CodVariavel = codVariavel,
                    CodReferenciaDestino = codReferenciaDestino,
                    CodClassificacao = codClassificacao,
                    Sexo = sexo,
                    ValorMin = valorMin,
                    ValorMax = valorMax,
                    IdadeMin = idadeMin,
                    IdadeMax = idadeMax,
                    Pagina = pagina,
                    CodUsuario = codUsuario,
                    Now = DateTime.Now
                },
                tx
            );
            inseridas++;
        }

        await tx.CommitAsync(ct);
        return new
        {
            inseridas,
            ignoradas,
            totalOrigem = origemRows.Count(),
            message = $"{inseridas} normalidade(s) importada(s). {ignoradas} registro(s) já existente(s) foram ignorados."
        };
    }

    private async Task<(string tipoAnexo, string? link, string? caminho)> ResolveAnexoFileAndTypeAsync(
        string? linkInput,
        UploadedFileContent? file,
        string? oldCaminho,
        string? oldLink
    )
    {
        var link = string.IsNullOrWhiteSpace(linkInput) ? null : linkInput.Trim();
        string? caminho = oldCaminho;
        var tipoAnexo = !string.IsNullOrWhiteSpace(link) ? "LINK" : "TEXTO";

        if (file != null)
        {
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext != ".pdf")
                throw new InvalidOperationException("Apenas arquivos PDF são permitidos.");

            DeletePhysicalFile(oldCaminho, oldLink);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(_staticUploadsRoot, fileName);
            await File.WriteAllBytesAsync(fullPath, file.Content);
            link = $"/static/uploads/{fileName}";
            caminho = null;
            tipoAnexo = "PDF";
        }

        return (tipoAnexo, link, caminho);
    }

    private void DeletePhysicalFile(string? caminho, string? link)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(caminho))
            {
                if (Path.IsPathRooted(caminho))
                {
                    if (File.Exists(caminho)) File.Delete(caminho);
                    return;
                }

                var fromUploads = Path.Combine(_staticUploadsRoot, Path.GetFileName(caminho));
                if (File.Exists(fromUploads))
                {
                    File.Delete(fromUploads);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(link) && link.StartsWith("/static/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(link);
                var full = Path.Combine(_staticUploadsRoot, fileName);
                if (File.Exists(full)) File.Delete(full);
            }
        }
        catch
        {
            // Ignora falhas de limpeza física para não bloquear fluxo principal.
        }
    }

    private static string? NormalizeAnexoUrl(string? link, string? caminho, string? tipo)
    {
        if (!string.IsNullOrWhiteSpace(link)) return link;
        if (string.IsNullOrWhiteSpace(caminho)) return null;
        if (tipo == "PDF" && !caminho.StartsWith("/static/", StringComparison.OrdinalIgnoreCase))
        {
            if (caminho.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                return $"/static/{caminho}";
            if (!caminho.StartsWith('/'))
                return $"/static/uploads/{caminho}";
            return $"/static{caminho}";
        }
        return caminho;
    }

    private static void ValidateReferencia(ReferenciaUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("Título é obrigatório.");
        if (req.Ano is < 1800 or > 2200)
            throw new InvalidOperationException("Ano inválido.");
    }

    private static void ValidateAnexo(AnexoUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Descricao))
            throw new InvalidOperationException("Descrição é obrigatória.");
        if (string.IsNullOrWhiteSpace(req.Nome))
            throw new InvalidOperationException("Nome é obrigatório.");
    }

    private static IDictionary<string, object?> AsDict(object row)
    {
        if (row is IDictionary<string, object?> d1) return d1;
        if (row is IDictionary<string, object> d2) return d2.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        return new Dictionary<string, object?>();
    }

    private static string? ToStr(IDictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) ? v?.ToString() : d.TryGetValue(key.ToLowerInvariant(), out var lv) ? lv?.ToString() : null;

    private static int ToInt(IDictionary<string, object?> d, string key)
        => ToNullableInt(d, key) ?? 0;

    private static int? ToNullableInt(IDictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) && !d.TryGetValue(key.ToLowerInvariant(), out v)) return null;
        if (v == null || v is DBNull) return null;
        if (v is int i) return i;
        if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static double? ToNullableDouble(IDictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) && !d.TryGetValue(key.ToLowerInvariant(), out v)) return null;
        if (v == null || v is DBNull) return null;
        if (v is double db) return db;
        if (v is decimal dec) return Convert.ToDouble(dec);
        if (double.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }
}

public static class UserClaimsExtensions
{
    public static int GetCodUsuario(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var cod) ? cod : 0;
    }
}
