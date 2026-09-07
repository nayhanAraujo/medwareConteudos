using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public interface IPadroesClienteNormalidadeService
{
    Task<PadroesClientePainelDto> GetPainelAsync(int codCliente, int? codPadrao, string? variavelBusca, CancellationToken ct);
    Task<int> CreatePadraoAsync(CreatePadraoClienteRequest req, int codUsuario, CancellationToken ct);
    Task UpdatePadraoAsync(int codPadrao, UpdatePadraoClienteRequest req, int codUsuario, CancellationToken ct);
    Task DeletePadraoAsync(int codPadrao, CancellationToken ct);
    Task SetVigenteAsync(int codPadrao, int codUsuario, CancellationToken ct);
    Task<ImportSnapshotResult> ImportarReferenciaAsync(int codPadrao, int codReferencia, int codUsuario, CancellationToken ct);
    Task<int> CreateFaixaAsync(int codPadrao, UpsertFaixaPadraoRequest req, int codUsuario, CancellationToken ct);
    Task UpdateFaixaAsync(int codFaixa, UpsertFaixaPadraoRequest req, int codUsuario, CancellationToken ct);
    Task DeleteFaixaAsync(int codFaixa, CancellationToken ct);
    Task UpsertComentarioAsync(
        int codPadrao,
        int codVariavel,
        string texto,
        int codUsuario,
        CancellationToken ct,
        string? sexo = "A",
        int? idadeMin = -1,
        int? idadeMax = -1
    );
}

public sealed class PadroesClienteNormalidadeService : IPadroesClienteNormalidadeService
{
    private readonly IFirebirdConnectionFactory _db;

    public PadroesClienteNormalidadeService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<PadroesClientePainelDto> GetPainelAsync(int codCliente, int? codPadrao, string? variavelBusca, CancellationToken ct)
    {
        if (codCliente < 1) throw new InvalidOperationException("Selecione um cliente válido.");

        await using var conn = await _db.OpenConnectionAsync(ct);

        var clienteExists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM Clientes WHERE CODCLIENTE = @codCliente", new { codCliente });
        if (clienteExists != 1)
            throw new InvalidOperationException("Cliente não encontrado.");

        var padroes = (await conn.QueryAsync<PadraoResumoDto>(@"
            SELECT
                P.CODPADRAO AS CodPadrao,
                P.NOME AS Nome,
                P.CODIGO AS Codigo,
                P.CODREFERENCIA AS CodReferencia,
                R.TITULO AS ReferenciaTitulo,
                P.PADRAOVIGENTE AS PadraoVigente,
                P.ATIVO AS Ativo,
                P.DESCRICAO AS Descricao,
                (SELECT COUNT(*) FROM PADRAONORMALIDADEFAIXA F WHERE F.CODPADRAO = P.CODPADRAO) AS TotalFaixas
            FROM CLIENTESPADRAONORMALIDADE P
            LEFT JOIN REFERENCIA R ON R.CODREFERENCIA = P.CODREFERENCIA
            WHERE P.CODCLIENTE = @codCliente
            ORDER BY P.PADRAOVIGENTE DESC, P.NOME", new { codCliente })).AsList();

        if (padroes.Count == 0)
        {
            var referencias = await LoadReferenciasLookupAsync(conn);
            return new PadroesClientePainelDto
            {
                CodCliente = codCliente,
                Padroes = [],
                Referencias = referencias,
                Filtros = new() { VariavelBusca = variavelBusca ?? "" }
            };
        }

        var vigente = padroes.FirstOrDefault(p => p.PadraoVigente == 1);
        var selectedId = codPadrao ?? vigente?.CodPadrao ?? padroes[0].CodPadrao;
        if (!padroes.Any(p => p.CodPadrao == selectedId))
            selectedId = padroes[0].CodPadrao;

        var selected = padroes.First(p => p.CodPadrao == selectedId);
        var variavelFilter = string.IsNullOrWhiteSpace(variavelBusca) ? null : $"%{variavelBusca.Trim()}%";

        var faixasSql = @"
            SELECT
                F.CODFAIXA AS CodFaixa,
                F.CODVARIAVEL AS CodVariavel,
                V.NOME AS NomeVariavel,
                V.VARIAVEL AS Variavel,
                V.SIGLA AS Sigla,
                F.SEXO AS Sexo,
                F.VALORMIN AS ValorMin,
                F.VALORMAX AS ValorMax,
                F.IDADE_MIN AS IdadeMin,
                F.IDADE_MAX AS IdadeMax,
                F.PAGINA_REFERENCIA AS Pagina,
                F.CODCLASSIFICACAO AS CodClassificacao,
                C.NOME AS Classificacao
            FROM PADRAONORMALIDADEFAIXA F
            JOIN VARIAVEIS V ON V.CODVARIAVEL = F.CODVARIAVEL
            LEFT JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = F.CODCLASSIFICACAO
            WHERE F.CODPADRAO = @selectedId";
        if (variavelFilter != null)
            faixasSql += " AND (V.NOME LIKE @variavelFilter OR V.VARIAVEL LIKE @variavelFilter OR V.SIGLA LIKE @variavelFilter)";

        faixasSql += " ORDER BY V.NOME, F.SEXO, F.VALORMIN";

        var faixas = (await conn.QueryAsync<FaixaPadraoDto>(faixasSql, new { selectedId, variavelFilter })).AsList();

        var comentarios = (await conn.QueryAsync<ComentarioPadraoDto>(@"
            SELECT CODVARIAVEL AS CodVariavel, SEXO AS Sexo, IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax,
                   TEXTO AS Texto, CODPADRAOCOMENTARIO AS CodPadraoComentario
            FROM PADRAONORMALIDADECOMENTARIO
            WHERE CODPADRAO = @selectedId", new { selectedId })).AsList();

        var comentariosPorVariavel = new Dictionary<string, ComentarioPadraoDto>();
        var comentariosPorSexoPorVariavel = new Dictionary<int, Dictionary<string, string>>();
        var comentariosPorIdadePorVariavel = new Dictionary<int, List<ComentarioPadraoIdadeDto>>();
        var rowsPorVariavel = new Dictionary<int, List<(string Sexo, int IdadeMin, int IdadeMax, string Texto)>>();
        foreach (var c in comentarios)
        {
            var sexo = string.IsNullOrWhiteSpace(c.Sexo) ? "A" : c.Sexo.Trim().ToUpperInvariant();
            if (sexo is not ("F" or "M" or "A")) sexo = "A";
            var idadeMin = c.IdadeMin < 0 ? -1 : c.IdadeMin;
            var idadeMax = c.IdadeMax < 0 ? -1 : c.IdadeMax;
            if (!comentariosPorSexoPorVariavel.TryGetValue(c.CodVariavel, out var porSexo))
            {
                porSexo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                comentariosPorSexoPorVariavel[c.CodVariavel] = porSexo;
            }
            var textoDisplay = Iso88591SafeText.ForDisplay(c.Texto);
            if (idadeMin < 0 && idadeMax < 0)
                porSexo[sexo] = textoDisplay;

            if (!comentariosPorIdadePorVariavel.TryGetValue(c.CodVariavel, out var porIdade))
            {
                porIdade = [];
                comentariosPorIdadePorVariavel[c.CodVariavel] = porIdade;
            }
            porIdade.Add(new ComentarioPadraoIdadeDto
            {
                Sexo = sexo,
                IdadeMin = idadeMin,
                IdadeMax = idadeMax,
                Texto = textoDisplay
            });

            if (!rowsPorVariavel.TryGetValue(c.CodVariavel, out var rowsList))
            {
                rowsList = [];
                rowsPorVariavel[c.CodVariavel] = rowsList;
            }
            rowsList.Add((sexo, idadeMin, idadeMax, textoDisplay));
            comentariosPorVariavel[c.CodVariavel.ToString()] = c;
        }
        var faixasPorVariavel = faixas
            .GroupBy(f => f.CodVariavel)
            .ToDictionary(
                g => g.Key.ToString(),
                g => g.Select(f => new FaixaPadraoResumoDto
                {
                    CodFaixa = f.CodFaixa,
                    Sexo = f.Sexo,
                    ValorMin = f.ValorMin,
                    ValorMax = f.ValorMax,
                    IdadeMin = f.IdadeMin,
                    IdadeMax = f.IdadeMax,
                    Pagina = f.Pagina,
                    CodClassificacao = f.CodClassificacao,
                    Classificacao = f.Classificacao
                }).ToList());

        var variaveis = faixas
            .GroupBy(f => f.CodVariavel)
            .Select(g =>
            {
                var first = g.First();
                comentariosPorSexoPorVariavel.TryGetValue(g.Key, out var porSexo);
                comentariosPorIdadePorVariavel.TryGetValue(g.Key, out var porIdade);
                rowsPorVariavel.TryGetValue(g.Key, out var rowsList);
                return new VariavelPadraoDto
                {
                    CodVariavel = g.Key,
                    NomeVariavel = first.NomeVariavel,
                    Variavel = first.Variavel,
                    Sigla = first.Sigla,
                    TotalFaixas = g.Count(),
                    ComentarioTexto = rowsList is null
                        ? FormatComentarioDisplay(porSexo)
                        : FormatComentarioDisplayComIdade(rowsList),
                    ComentariosPorSexo = porSexo,
                    ComentariosPorIdade = porIdade
                };
            })
            .OrderBy(v => v.NomeVariavel)
            .ToList();

        // Variáveis só com comentário (sem faixa)
        var commentOnlyIds = comentariosPorSexoPorVariavel.Keys
            .Concat(comentariosPorIdadePorVariavel.Keys)
            .Distinct()
            .Where(id => !faixasPorVariavel.ContainsKey(id.ToString()));
        foreach (var codVar in commentOnlyIds)
        {
            comentariosPorSexoPorVariavel.TryGetValue(codVar, out var porSexo);
            comentariosPorIdadePorVariavel.TryGetValue(codVar, out var porIdade);
            rowsPorVariavel.TryGetValue(codVar, out var rowsList);
            var varInfo = await conn.QueryFirstOrDefaultAsync<(string Nome, string? Variavel, string? Sigla)>(@"
                SELECT NOME, VARIAVEL, SIGLA FROM VARIAVEIS WHERE CODVARIAVEL = @id", new { id = codVar });
            if (string.IsNullOrWhiteSpace(varInfo.Nome)) continue;
            if (variavelFilter != null
                && !varInfo.Nome.Contains(variavelBusca!, StringComparison.OrdinalIgnoreCase)
                && !(varInfo.Variavel?.Contains(variavelBusca!, StringComparison.OrdinalIgnoreCase) ?? false)
                && !(varInfo.Sigla?.Contains(variavelBusca!, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;

            variaveis.Add(new VariavelPadraoDto
            {
                CodVariavel = codVar,
                NomeVariavel = varInfo.Nome,
                Variavel = varInfo.Variavel,
                Sigla = varInfo.Sigla,
                TotalFaixas = 0,
                ComentarioTexto = rowsList is null
                    ? FormatComentarioDisplay(porSexo)
                    : FormatComentarioDisplayComIdade(rowsList),
                ComentariosPorSexo = porSexo,
                ComentariosPorIdade = porIdade
            });
            faixasPorVariavel[codVar.ToString()] = [];
        }

        var referenciasLookup = await LoadReferenciasLookupAsync(conn);

        return new PadroesClientePainelDto
        {
            CodCliente = codCliente,
            Padroes = padroes,
            PadraoSelecionado = selected,
            Variaveis = variaveis,
            FaixasPorVariavel = faixasPorVariavel,
            ComentariosPorVariavel = comentariosPorIdadePorVariavel.Keys
                .Concat(comentariosPorSexoPorVariavel.Keys)
                .Distinct()
                .ToDictionary(
                    id => id.ToString(),
                    id =>
                    {
                        comentariosPorSexoPorVariavel.TryGetValue(id, out var porSexo);
                        porSexo ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        comentariosPorIdadePorVariavel.TryGetValue(id, out var porIdade);
                        rowsPorVariavel.TryGetValue(id, out var rowsList);
                        return new ComentarioPadraoResumoDto
                        {
                            CodPadraoComentario = comentariosPorVariavel.TryGetValue(id.ToString(), out var c) ? c.CodPadraoComentario : 0,
                            Texto = rowsList is null
                                ? FormatComentarioDisplay(porSexo) ?? ""
                                : FormatComentarioDisplayComIdade(rowsList) ?? "",
                            Sexo = porSexo.ContainsKey("A") ? "A" : porSexo.Keys.FirstOrDefault() ?? "A",
                            ComentariosPorSexo = porSexo,
                            ComentariosPorIdade = porIdade
                        };
                    }),
            Referencias = referenciasLookup,
            Filtros = new() { VariavelBusca = variavelBusca ?? "" }
        };
    }

    public async Task<int> CreatePadraoAsync(CreatePadraoClienteRequest req, int codUsuario, CancellationToken ct)
    {
        ValidatePadraoHeader(req.CodCliente, req.Nome, req.Codigo);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var agora = DateTime.UtcNow;
        var codPadrao = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO CLIENTESPADRAONORMALIDADE
                (CODCLIENTE, NOME, CODIGO, CODREFERENCIA, PADRAOVIGENTE, ATIVO, DESCRICAO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES
                (@CodCliente, @Nome, @Codigo, @CodReferencia, @PadraoVigente, @Ativo, @Descricao, @CodUsuario, @Agora)
            RETURNING CODPADRAO",
            new
            {
                req.CodCliente,
                Nome = req.Nome.Trim(),
                Codigo = NormalizeCodigo(req.Codigo),
                req.CodReferencia,
                PadraoVigente = (short)(req.PadraoVigente ? 1 : 0),
                Ativo = (short)(req.Ativo != false ? 1 : 0),
                Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim(),
                CodUsuario = codUsuario,
                Agora = agora
            }, tx);

        if (req.PadraoVigente)
            await ClearOtherVigenteAsync(conn, tx, req.CodCliente, codPadrao);

        if (req.CodReferencia.HasValue && req.CodReferencia > 0)
            await ImportSnapshotInternalAsync(conn, tx, codPadrao, req.CodReferencia.Value, codUsuario, agora, replace: false);

        await tx.CommitAsync(ct);
        return codPadrao;
    }

    public async Task UpdatePadraoAsync(int codPadrao, UpdatePadraoClienteRequest req, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var current = await conn.QueryFirstOrDefaultAsync<(int CodCliente, string Codigo)>(@"
            SELECT CODCLIENTE, CODIGO FROM CLIENTESPADRAONORMALIDADE WHERE CODPADRAO = @codPadrao", new { codPadrao });
        if (current.CodCliente == 0)
            throw new InvalidOperationException("Padrão não encontrado.");

        if (!string.IsNullOrWhiteSpace(req.Codigo) && NormalizeCodigo(req.Codigo) != current.Codigo)
        {
            var dup = await conn.ExecuteScalarAsync<int?>(@"
                SELECT 1 FROM CLIENTESPADRAONORMALIDADE
                WHERE CODCLIENTE = @codCliente AND CODIGO = @codigo AND CODPADRAO <> @codPadrao",
                new { codCliente = current.CodCliente, codigo = NormalizeCodigo(req.Codigo), codPadrao });
            if (dup == 1) throw new InvalidOperationException("Já existe um padrão com este código para o cliente.");
        }

        await using var tx = await conn.BeginTransactionAsync(ct);
        var agora = DateTime.UtcNow;

        await conn.ExecuteAsync(@"
            UPDATE CLIENTESPADRAONORMALIDADE SET
                NOME = COALESCE(@Nome, NOME),
                CODIGO = COALESCE(@Codigo, CODIGO),
                CODREFERENCIA = @CodReferencia,
                ATIVO = COALESCE(@Ativo, ATIVO),
                DESCRICAO = @Descricao,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Agora
            WHERE CODPADRAO = @codPadrao",
            new
            {
                codPadrao,
                Nome = string.IsNullOrWhiteSpace(req.Nome) ? null : req.Nome.Trim(),
                Codigo = string.IsNullOrWhiteSpace(req.Codigo) ? null : NormalizeCodigo(req.Codigo),
                req.CodReferencia,
                Ativo = req.Ativo.HasValue ? (short?)(req.Ativo.Value ? 1 : 0) : null,
                Descricao = req.Descricao,
                CodUsuario = codUsuario,
                Agora = agora
            }, tx);

        if (req.PadraoVigente == true)
        {
            await ClearOtherVigenteAsync(conn, tx, current.CodCliente, codPadrao);
            await conn.ExecuteAsync(
                "UPDATE CLIENTESPADRAONORMALIDADE SET PADRAOVIGENTE = 1 WHERE CODPADRAO = @codPadrao",
                new { codPadrao }, tx);
        }
        else if (req.PadraoVigente == false)
        {
            await conn.ExecuteAsync(
                "UPDATE CLIENTESPADRAONORMALIDADE SET PADRAOVIGENTE = 0 WHERE CODPADRAO = @codPadrao",
                new { codPadrao }, tx);
        }

        await tx.CommitAsync(ct);
    }

    public async Task DeletePadraoAsync(int codPadrao, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(
            "DELETE FROM CLIENTESPADRAONORMALIDADE WHERE CODPADRAO = @codPadrao", new { codPadrao });
        if (affected == 0) throw new InvalidOperationException("Padrão não encontrado.");
    }

    public async Task SetVigenteAsync(int codPadrao, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var codCliente = await conn.ExecuteScalarAsync<int?>(
            "SELECT CODCLIENTE FROM CLIENTESPADRAONORMALIDADE WHERE CODPADRAO = @codPadrao", new { codPadrao });
        if (!codCliente.HasValue) throw new InvalidOperationException("Padrão não encontrado.");

        await using var tx = await conn.BeginTransactionAsync(ct);
        await ClearOtherVigenteAsync(conn, tx, codCliente.Value, codPadrao);
        await conn.ExecuteAsync(@"
            UPDATE CLIENTESPADRAONORMALIDADE SET
                PADRAOVIGENTE = 1, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
            WHERE CODPADRAO = @codPadrao",
            new { codPadrao, codUsuario, agora = DateTime.UtcNow }, tx);
        await tx.CommitAsync(ct);
    }

    public async Task<ImportSnapshotResult> ImportarReferenciaAsync(int codPadrao, int codReferencia, int codUsuario, CancellationToken ct)
    {
        if (codReferencia < 1) throw new InvalidOperationException("Referência inválida.");

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM CLIENTESPADRAONORMALIDADE WHERE CODPADRAO = @codPadrao", new { codPadrao });
        if (exists != 1) throw new InvalidOperationException("Padrão não encontrado.");

        var refExists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM REFERENCIA WHERE CODREFERENCIA = @codReferencia", new { codReferencia });
        if (refExists != 1) throw new InvalidOperationException("Referência não encontrada.");

        await using var tx = await conn.BeginTransactionAsync(ct);
        var agora = DateTime.UtcNow;
        var (faixas, comentarios) = await ImportSnapshotInternalAsync(conn, tx, codPadrao, codReferencia, codUsuario, agora, replace: true);

        await conn.ExecuteAsync(@"
            UPDATE CLIENTESPADRAONORMALIDADE SET CODREFERENCIA = @codReferencia, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
            WHERE CODPADRAO = @codPadrao",
            new { codPadrao, codReferencia, codUsuario, agora }, tx);

        await tx.CommitAsync(ct);
        return new ImportSnapshotResult
        {
            FaixasImportadas = faixas,
            ComentariosImportados = comentarios,
            Message = $"Snapshot importado: {faixas} faixa(s), {comentarios} comentário(s)."
        };
    }

    public async Task<int> CreateFaixaAsync(int codPadrao, UpsertFaixaPadraoRequest req, int codUsuario, CancellationToken ct)
    {
        ValidateFaixa(req);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await EnsurePadraoExistsAsync(conn, codPadrao);

        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO PADRAONORMALIDADEFAIXA
                (CODPADRAO, CODVARIAVEL, CODCLASSIFICACAO, SEXO, VALORMIN, VALORMAX, IDADE_MIN, IDADE_MAX, PAGINA_REFERENCIA, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES
                (@codPadrao, @CodVariavel, @CodClassificacao, @Sexo, @ValorMin, @ValorMax, @IdadeMin, @IdadeMax, @Pagina, @CodUsuario, @Agora)
            RETURNING CODFAIXA",
            new
            {
                codPadrao,
                req.CodVariavel,
                req.CodClassificacao,
                Sexo = NormalizeSexo(req.Sexo),
                req.ValorMin,
                req.ValorMax,
                req.IdadeMin,
                req.IdadeMax,
                Pagina = req.Pagina,
                CodUsuario = codUsuario,
                Agora = DateTime.UtcNow
            });
    }

    public async Task UpdateFaixaAsync(int codFaixa, UpsertFaixaPadraoRequest req, int codUsuario, CancellationToken ct)
    {
        ValidateFaixa(req);
        await using var conn = await _db.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(@"
            UPDATE PADRAONORMALIDADEFAIXA SET
                CODVARIAVEL = COALESCE(@CodVariavel, CODVARIAVEL),
                CODCLASSIFICACAO = @CodClassificacao,
                SEXO = COALESCE(@Sexo, SEXO),
                VALORMIN = @ValorMin,
                VALORMAX = @ValorMax,
                IDADE_MIN = @IdadeMin,
                IDADE_MAX = @IdadeMax,
                PAGINA_REFERENCIA = @Pagina,
                CODUSUARIO = @CodUsuario,
                DTHRULTMODIFICACAO = @Agora
            WHERE CODFAIXA = @codFaixa",
            new
            {
                codFaixa,
                req.CodVariavel,
                req.CodClassificacao,
                Sexo = string.IsNullOrWhiteSpace(req.Sexo) ? null : NormalizeSexo(req.Sexo),
                req.ValorMin,
                req.ValorMax,
                req.IdadeMin,
                req.IdadeMax,
                Pagina = req.Pagina,
                CodUsuario = codUsuario,
                Agora = DateTime.UtcNow
            });
        if (affected == 0) throw new InvalidOperationException("Faixa não encontrada.");
    }

    public async Task DeleteFaixaAsync(int codFaixa, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(
            "DELETE FROM PADRAONORMALIDADEFAIXA WHERE CODFAIXA = @codFaixa", new { codFaixa });
        if (affected == 0) throw new InvalidOperationException("Faixa não encontrada.");
    }

    public async Task UpsertComentarioAsync(
        int codPadrao,
        int codVariavel,
        string texto,
        int codUsuario,
        CancellationToken ct,
        string? sexo = "A",
        int? idadeMin = -1,
        int? idadeMax = -1
    )
    {
        if (codVariavel < 1) throw new InvalidOperationException("Variável inválida.");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await EnsurePadraoExistsAsync(conn, codPadrao);

        var sexoNorm = NormalizeSexoComentario(sexo);
        var imin = idadeMin is null or < 0 ? -1 : idadeMin.Value;
        var imax = idadeMax is null or < 0 ? -1 : idadeMax.Value;
        var agora = DateTime.UtcNow;
        var trimmed = Iso88591SafeText.ForStorage((texto ?? "").Trim());
        if (string.IsNullOrEmpty(trimmed))
        {
            await conn.ExecuteAsync(
                @"DELETE FROM PADRAONORMALIDADECOMENTARIO
                  WHERE CODPADRAO = @codPadrao AND CODVARIAVEL = @codVariavel
                    AND SEXO = @sexoNorm AND IDADE_MIN = @imin AND IDADE_MAX = @imax",
                new { codPadrao, codVariavel, sexoNorm, imin, imax });
            return;
        }

        if (trimmed.Length > 500) trimmed = trimmed[..500];

        await conn.ExecuteAsync(@"
            UPDATE OR INSERT INTO PADRAONORMALIDADECOMENTARIO
                (CODPADRAO, CODVARIAVEL, SEXO, IDADE_MIN, IDADE_MAX, TEXTO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES
                (@codPadrao, @codVariavel, @sexoNorm, @imin, @imax, @trimmed, @codUsuario, @agora)
            MATCHING (CODPADRAO, CODVARIAVEL, SEXO, IDADE_MIN, IDADE_MAX)",
            new { codPadrao, codVariavel, sexoNorm, imin, imax, trimmed, codUsuario, agora });
    }

    private static string NormalizeSexoComentario(string? sexo)
    {
        var s = (sexo ?? "A").Trim().ToUpperInvariant();
        return s is "F" or "M" or "A" ? s : "A";
    }

    private static string? FormatComentarioDisplay(IReadOnlyDictionary<string, string>? porSexo)
    {
        if (porSexo is null || porSexo.Count == 0) return null;
        if (porSexo.TryGetValue("A", out var a) && !string.IsNullOrWhiteSpace(a))
            return a;
        var parts = new List<string>();
        if (porSexo.TryGetValue("F", out var f) && !string.IsNullOrWhiteSpace(f))
            parts.Add($"F: {f}");
        if (porSexo.TryGetValue("M", out var m) && !string.IsNullOrWhiteSpace(m))
            parts.Add($"M: {m}");
        return parts.Count > 0 ? string.Join("; ", parts) : null;
    }

    private static string? FormatComentarioDisplayComIdade(
        IReadOnlyList<(string Sexo, int IdadeMin, int IdadeMax, string Texto)> rows
    )
    {
        var comIdade = rows.Where(r => r.IdadeMin >= 0 || r.IdadeMax >= 0).ToList();
        if (comIdade.Count > 0)
        {
            return string.Join(
                "; ",
                comIdade.OrderBy(r => r.IdadeMin).Select(r => $"{r.IdadeMin}-{r.IdadeMax}: {r.Texto}")
            );
        }

        var porSexo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
            porSexo[r.Sexo] = r.Texto;
        return FormatComentarioDisplay(porSexo);
    }

    private static async Task EnsurePadraoExistsAsync(System.Data.Common.DbConnection conn, int codPadrao)
    {
        var exists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM CLIENTESPADRAONORMALIDADE WHERE CODPADRAO = @codPadrao", new { codPadrao });
        if (exists != 1) throw new InvalidOperationException("Padrão não encontrado.");
    }

    private static async Task<(int Faixas, int Comentarios)> ImportSnapshotInternalAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        int codPadrao,
        int codReferencia,
        int codUsuario,
        DateTime agora,
        bool replace)
    {
        if (replace)
        {
            await conn.ExecuteAsync("DELETE FROM PADRAONORMALIDADECOMENTARIO WHERE CODPADRAO = @codPadrao", new { codPadrao }, tx);
            await conn.ExecuteAsync("DELETE FROM PADRAONORMALIDADEFAIXA WHERE CODPADRAO = @codPadrao", new { codPadrao }, tx);
        }

        var faixas = await conn.ExecuteAsync(@"
            INSERT INTO PADRAONORMALIDADEFAIXA
                (CODPADRAO, CODVARIAVEL, CODCLASSIFICACAO, SEXO, VALORMIN, VALORMAX, IDADE_MIN, IDADE_MAX, PAGINA_REFERENCIA, CODUSUARIO, DTHRULTMODIFICACAO)
            SELECT
                @codPadrao, CODVARIAVEL, CODCLASSIFICACAO, SEXO, VALORMIN, VALORMAX, IDADE_MIN, IDADE_MAX, PAGINA_REFERENCIA, @codUsuario, @agora
            FROM NORMALIDADE
            WHERE CODREFERENCIA = @codReferencia",
            new { codPadrao, codReferencia, codUsuario, agora }, tx);

        var comentarios = await conn.ExecuteAsync(@"
            INSERT INTO PADRAONORMALIDADECOMENTARIO (CODPADRAO, CODVARIAVEL, SEXO, IDADE_MIN, IDADE_MAX, TEXTO, CODUSUARIO, DTHRULTMODIFICACAO)
            SELECT @codPadrao, CODVARIAVEL, SEXO, IDADE_MIN, IDADE_MAX, TEXTO, @codUsuario, @agora
            FROM NORMALIDADECOMENTARIO
            WHERE CODREFERENCIA = @codReferencia",
            new { codPadrao, codReferencia, codUsuario, agora }, tx);

        return (faixas, comentarios);
    }

    private static async Task ClearOtherVigenteAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        int codCliente,
        int exceptCodPadrao)
    {
        await conn.ExecuteAsync(@"
            UPDATE CLIENTESPADRAONORMALIDADE SET PADRAOVIGENTE = 0
            WHERE CODCLIENTE = @codCliente AND CODPADRAO <> @exceptCodPadrao",
            new { codCliente, exceptCodPadrao }, tx);
    }

    private static async Task<List<ReferenciaLookupDto>> LoadReferenciasLookupAsync(System.Data.Common.DbConnection conn)
    {
        var rows = await conn.QueryAsync<ReferenciaLookupDto>(@"
            SELECT CODREFERENCIA AS Codigo, TITULO AS Titulo, ANO AS Ano
            FROM REFERENCIA ORDER BY TITULO");
        return rows.AsList();
    }

    private static void ValidatePadraoHeader(int codCliente, string? nome, string? codigo)
    {
        if (codCliente < 1) throw new InvalidOperationException("Cliente inválido.");
        if (string.IsNullOrWhiteSpace(nome)) throw new InvalidOperationException("Informe o nome do padrão.");
        if (string.IsNullOrWhiteSpace(codigo)) throw new InvalidOperationException("Informe o código do padrão.");
    }

    private static void ValidateFaixa(UpsertFaixaPadraoRequest req)
    {
        if (req.CodVariavel < 1) throw new InvalidOperationException("Variável inválida.");
        if (string.IsNullOrWhiteSpace(req.Sexo)) throw new InvalidOperationException("Informe o sexo (M, F ou A).");
    }

    private static string NormalizeCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant().Replace(' ', '_');

    private static string NormalizeSexo(string sexo)
    {
        var s = sexo.Trim().ToUpperInvariant();
        return s == "U" ? "A" : s;
    }
}

public sealed class PadroesClientePainelDto
{
    public int CodCliente { get; set; }
    public List<PadraoResumoDto> Padroes { get; set; } = [];
    public PadraoResumoDto? PadraoSelecionado { get; set; }
    public List<VariavelPadraoDto> Variaveis { get; set; } = [];
    public Dictionary<string, List<FaixaPadraoResumoDto>> FaixasPorVariavel { get; set; } = [];
    public Dictionary<string, ComentarioPadraoResumoDto> ComentariosPorVariavel { get; set; } = [];
    public List<ReferenciaLookupDto> Referencias { get; set; } = [];
    public PadroesClienteFiltrosDto Filtros { get; set; } = new();
}

public sealed class PadroesClienteFiltrosDto
{
    public string VariavelBusca { get; set; } = "";
}

public sealed class PadraoResumoDto
{
    public int CodPadrao { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public int? CodReferencia { get; set; }
    public string? ReferenciaTitulo { get; set; }
    public int PadraoVigente { get; set; }
    public int Ativo { get; set; }
    public string? Descricao { get; set; }
    public int TotalFaixas { get; set; }
}

public sealed class VariavelPadraoDto
{
    public int CodVariavel { get; set; }
    public string NomeVariavel { get; set; } = "";
    public string? Variavel { get; set; }
    public string? Sigla { get; set; }
    public int TotalFaixas { get; set; }
    public string? ComentarioTexto { get; set; }
    public Dictionary<string, string>? ComentariosPorSexo { get; set; }
    public List<ComentarioPadraoIdadeDto>? ComentariosPorIdade { get; set; }
}

public sealed class FaixaPadraoDto
{
    public int CodFaixa { get; set; }
    public int CodVariavel { get; set; }
    public string NomeVariavel { get; set; } = "";
    public string? Variavel { get; set; }
    public string? Sigla { get; set; }
    public string? Sexo { get; set; }
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public int? IdadeMin { get; set; }
    public int? IdadeMax { get; set; }
    public decimal? Pagina { get; set; }
    public int? CodClassificacao { get; set; }
    public string? Classificacao { get; set; }
}

public sealed class FaixaPadraoResumoDto
{
    public int CodFaixa { get; set; }
    public string? Sexo { get; set; }
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public int? IdadeMin { get; set; }
    public int? IdadeMax { get; set; }
    public decimal? Pagina { get; set; }
    public int? CodClassificacao { get; set; }
    public string? Classificacao { get; set; }
}

public sealed class ComentarioPadraoDto
{
    public int CodVariavel { get; set; }
    public string Sexo { get; set; } = "A";
    public int IdadeMin { get; set; } = -1;
    public int IdadeMax { get; set; } = -1;
    public string Texto { get; set; } = "";
    public int CodPadraoComentario { get; set; }
}

public sealed class ComentarioPadraoIdadeDto
{
    public string Sexo { get; set; } = "A";
    public int IdadeMin { get; set; } = -1;
    public int IdadeMax { get; set; } = -1;
    public string Texto { get; set; } = "";
}

public sealed class ComentarioPadraoResumoDto
{
    public int CodPadraoComentario { get; set; }
    public string Texto { get; set; } = "";
    public string Sexo { get; set; } = "A";
    public Dictionary<string, string>? ComentariosPorSexo { get; set; }
    public List<ComentarioPadraoIdadeDto>? ComentariosPorIdade { get; set; }
}

public sealed class ReferenciaLookupDto
{
    public int Codigo { get; set; }
    public string Titulo { get; set; } = "";
    public int? Ano { get; set; }
}

public sealed class CreatePadraoClienteRequest
{
    public int CodCliente { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public int? CodReferencia { get; set; }
    public bool PadraoVigente { get; set; }
    public bool? Ativo { get; set; } = true;
    public string? Descricao { get; set; }
}

public sealed class UpdatePadraoClienteRequest
{
    public string? Nome { get; set; }
    public string? Codigo { get; set; }
    public int? CodReferencia { get; set; }
    public bool? PadraoVigente { get; set; }
    public bool? Ativo { get; set; }
    public string? Descricao { get; set; }
}

public sealed class UpsertFaixaPadraoRequest
{
    public int CodVariavel { get; set; }
    public string Sexo { get; set; } = "A";
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public int? IdadeMin { get; set; }
    public int? IdadeMax { get; set; }
    public decimal? Pagina { get; set; }
    public int? CodClassificacao { get; set; }
}

public sealed class ImportSnapshotResult
{
    public int FaixasImportadas { get; set; }
    public int ComentariosImportados { get; set; }
    public string Message { get; set; } = "";
}
