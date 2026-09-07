using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public interface INormalidadeUnidadeConversaoService
{
    Task<ConversaoUnidadePreviewDto> PreviewAsync(ConversaoUnidadeRequest req, CancellationToken ct);
    Task<ConversaoUnidadeApplyResultDto> ApplyAsync(ConversaoUnidadeApplyRequest req, int codUsuario, CancellationToken ct);
    IReadOnlyList<object> ListParesSuportados();
}

public sealed class NormalidadeUnidadeConversaoService : INormalidadeUnidadeConversaoService
{
    private readonly IFirebirdConnectionFactory _db;

    public NormalidadeUnidadeConversaoService(IFirebirdConnectionFactory db) => _db = db;

    public IReadOnlyList<object> ListParesSuportados() =>
        UnidadeConversaoCatalog.ListPares()
            .Select(p => new { de = p.De, para = p.Para, fator = p.Fator })
            .Cast<object>()
            .ToList();

    public async Task<ConversaoUnidadePreviewDto> PreviewAsync(ConversaoUnidadeRequest req, CancellationToken ct)
    {
        ValidateRequest(req);
        await using var conn = await _db.OpenConnectionAsync(ct);

        var variavel = await LoadVariavelAsync(conn, req.CodVariavel)
            ?? throw new InvalidOperationException("Variável não encontrada.");

        var unidadeDestino = await ResolveUnidadeAsync(conn, req.UnidadeDestino)
            ?? throw new InvalidOperationException($"Unidade destino '{req.UnidadeDestino}' não cadastrada em UNIDADEMEDIDA.");

        var unidadeOrigemDesc = string.IsNullOrWhiteSpace(req.UnidadeOrigem)
            ? (variavel.UnidadeDescricao ?? "")
            : req.UnidadeOrigem.Trim();

        if (string.IsNullOrWhiteSpace(unidadeOrigemDesc))
            throw new InvalidOperationException("Informe a unidade de origem (a variável não tem unidade cadastrada).");

        if (!UnidadeConversaoCatalog.TryGetFactor(unidadeOrigemDesc, unidadeDestino.Descricao, out var fator))
            throw new InvalidOperationException(
                $"Par de unidades não suportado no v1: '{unidadeOrigemDesc}' → '{unidadeDestino.Descricao}'. " +
                "Suportados: mm↔cm, m/s↔cm/s.");

        var casas = req.CasasDecimais ?? Math.Max(0, variavel.CasasDecimais);
        // Após m/s→cm/s, tipicamente 0–1 casa; se origem tinha muitas casas, manter pelo menos as atuais
        if (UnidadeConversaoCatalog.NormalizeDescricao(unidadeOrigemDesc) == "m/s"
            && UnidadeConversaoCatalog.NormalizeDescricao(unidadeDestino.Descricao) == "cm/s"
            && !req.CasasDecimais.HasValue)
            casas = Math.Max(casas, 1);

        var faixas = await LoadFaixasAsync(conn, req);
        var comentarios = await LoadComentariosAsync(conn, req);

        var faixasPrev = new List<ConversaoFaixaPreviewDto>();
        foreach (var f in faixas)
        {
            var classe = UnidadeConversaoCatalog.ClassificarFaixa(
                unidadeOrigemDesc, unidadeDestino.Descricao, f.ValorMin, f.ValorMax);
            var incluirDefault = classe == "compativel_origem";
            faixasPrev.Add(new ConversaoFaixaPreviewDto
            {
                Id = f.Id,
                Tipo = f.Tipo,
                Sexo = f.Sexo,
                IdadeMin = f.IdadeMin,
                IdadeMax = f.IdadeMax,
                ValorMinAtual = f.ValorMin,
                ValorMaxAtual = f.ValorMax,
                ValorMinNovo = UnidadeConversaoCatalog.ApplyFactor(f.ValorMin, fator, casas),
                ValorMaxNovo = UnidadeConversaoCatalog.ApplyFactor(f.ValorMax, fator, casas),
                Classificacao = classe,
                IncluirDefault = incluirDefault
            });
        }

        var comentariosPrev = new List<ConversaoComentarioPreviewDto>();
        var naoConvertidos = new List<string>();
        foreach (var c in comentarios)
        {
            var classe = ComentarioNumericoParser.ClassificarComentario(
                unidadeOrigemDesc, unidadeDestino.Descricao, c.Texto);
            var parse = ComentarioNumericoParser.TryConvert(c.Texto, fator, casas);
            var incluirDefault = classe == "compativel_origem" && parse.Ok;
            if (!parse.Ok)
                naoConvertidos.Add($"{c.Tipo}#{c.Id}: {c.Texto} ({parse.Motivo})");

            comentariosPrev.Add(new ConversaoComentarioPreviewDto
            {
                Id = c.Id,
                Tipo = c.Tipo,
                Sexo = c.Sexo,
                IdadeMin = c.IdadeMin,
                IdadeMax = c.IdadeMax,
                TextoAtual = c.Texto,
                TextoNovo = parse.Ok ? parse.TextoConvertido : c.Texto,
                Convertivel = parse.Ok,
                Motivo = parse.Motivo,
                Classificacao = classe,
                IncluirDefault = incluirDefault
            });
        }

        return new ConversaoUnidadePreviewDto
        {
            CodVariavel = variavel.CodVariavel,
            NomeVariavel = variavel.Nome,
            CodigoVariavel = variavel.Variavel,
            UnidadeOrigem = unidadeOrigemDesc,
            UnidadeDestino = unidadeDestino.Descricao,
            CodUnidadeDestino = unidadeDestino.CodUnidadeMedida,
            CodUnidadeOrigemAtual = variavel.CodUnidadeMedida,
            Fator = fator,
            CasasDecimais = casas,
            AtualizarUnidadeVariavel = req.CodPadrao is null or < 1,
            Faixas = faixasPrev,
            Comentarios = comentariosPrev,
            NaoConvertidos = naoConvertidos,
            Resumo = new ConversaoUnidadeResumoDto
            {
                TotalFaixas = faixasPrev.Count,
                FaixasIncluidasDefault = faixasPrev.Count(x => x.IncluirDefault),
                FaixasJaDestino = faixasPrev.Count(x => x.Classificacao == "ja_parece_destino"),
                TotalComentarios = comentariosPrev.Count,
                ComentariosIncluidosDefault = comentariosPrev.Count(x => x.IncluirDefault),
                ComentariosNaoConvertiveis = comentariosPrev.Count(x => !x.Convertivel)
            }
        };
    }

    public async Task<ConversaoUnidadeApplyResultDto> ApplyAsync(
        ConversaoUnidadeApplyRequest req,
        int codUsuario,
        CancellationToken ct)
    {
        if (req.Preview is null)
            throw new InvalidOperationException("Informe o preview confirmado.");

        var preview = await PreviewAsync(req.Preview, ct);
        var faixasIds = new HashSet<int>(req.FaixaIds ?? preview.Faixas.Where(f => f.IncluirDefault).Select(f => f.Id));
        var comentarioIds = new HashSet<int>(
            req.ComentarioIds ?? preview.Comentarios.Where(c => c.IncluirDefault).Select(c => c.Id));

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var agora = DateTime.Now;
        var faixasOk = 0;
        var comentariosOk = 0;

        foreach (var f in preview.Faixas.Where(x => faixasIds.Contains(x.Id)))
        {
            if (f.Tipo == "padrao")
            {
                await conn.ExecuteAsync(@"
                    UPDATE PADRAONORMALIDADEFAIXA
                    SET VALORMIN = @vmin, VALORMAX = @vmax, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
                    WHERE CODFAIXA = @id",
                    new { vmin = f.ValorMinNovo, vmax = f.ValorMaxNovo, codUsuario, agora, id = f.Id }, tx);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE NORMALIDADE
                    SET VALORMIN = @vmin, VALORMAX = @vmax, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
                    WHERE CODNORMALIDADE = @id",
                    new { vmin = f.ValorMinNovo, vmax = f.ValorMaxNovo, codUsuario, agora, id = f.Id }, tx);
            }
            faixasOk++;
        }

        foreach (var c in preview.Comentarios.Where(x => comentarioIds.Contains(x.Id) && x.Convertivel))
        {
            var texto = Iso88591SafeText.ForStorage(c.TextoNovo ?? "");
            if (texto.Length > 500) texto = texto[..500];
            if (c.Tipo == "padrao")
            {
                await conn.ExecuteAsync(@"
                    UPDATE PADRAONORMALIDADECOMENTARIO
                    SET TEXTO = @texto, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
                    WHERE CODPADRAOCOMENTARIO = @id",
                    new { texto, codUsuario, agora, id = c.Id }, tx);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE NORMALIDADECOMENTARIO
                    SET TEXTO = @texto, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
                    WHERE CODNORMALIDADECOMENTARIO = @id",
                    new { texto, codUsuario, agora, id = c.Id }, tx);
            }
            comentariosOk++;
        }

        var unidadeAtualizada = false;
        if (preview.AtualizarUnidadeVariavel
            && (req.AtualizarUnidadeVariavel ?? true)
            && preview.CodUnidadeDestino > 0
            && preview.CodUnidadeDestino != preview.CodUnidadeOrigemAtual)
        {
            await conn.ExecuteAsync(@"
                UPDATE VARIAVEIS
                SET CODUNIDADEMEDIDA = @codUnidade, CODUSUARIO = @codUsuario, DTHRULTMODIFICACAO = @agora
                WHERE CODVARIAVEL = @codVariavel",
                new
                {
                    codUnidade = preview.CodUnidadeDestino,
                    codUsuario,
                    agora,
                    codVariavel = preview.CodVariavel
                }, tx);
            unidadeAtualizada = true;
        }

        await tx.CommitAsync(ct);

        return new ConversaoUnidadeApplyResultDto
        {
            FaixasAtualizadas = faixasOk,
            ComentariosAtualizados = comentariosOk,
            UnidadeVariavelAtualizada = unidadeAtualizada,
            CodUnidadeDestino = preview.CodUnidadeDestino,
            UnidadeDestino = preview.UnidadeDestino,
            Message = $"{faixasOk} faixa(s) e {comentariosOk} comentário(s) convertidos"
                + (unidadeAtualizada ? $"; unidade da variável → {preview.UnidadeDestino}." : ".")
        };
    }

    private static void ValidateRequest(ConversaoUnidadeRequest req)
    {
        if (req.CodVariavel < 1) throw new InvalidOperationException("Informe a variável.");
        if (string.IsNullOrWhiteSpace(req.UnidadeDestino))
            throw new InvalidOperationException("Informe a unidade destino.");
        if (req.CodPadrao is > 0 && req.CodReferencia is > 0)
            throw new InvalidOperationException("Informe só codReferencia ou só codPadrao, não ambos.");
    }

    private static async Task<VariavelUnidadeRow?> LoadVariavelAsync(FbConnection conn, int codVariavel)
    {
        return await conn.QueryFirstOrDefaultAsync<VariavelUnidadeRow>(@"
            SELECT v.CODVARIAVEL AS CodVariavel, v.NOME AS Nome, v.VARIAVEL AS Variavel,
                   v.CODUNIDADEMEDIDA AS CodUnidadeMedida,
                   COALESCE(v.CASASDECIMAIS, 0) AS CasasDecimais,
                   u.DESCRICAO AS UnidadeDescricao
            FROM VARIAVEIS v
            LEFT JOIN UNIDADEMEDIDA u ON u.CODUNIDADEMEDIDA = v.CODUNIDADEMEDIDA
            WHERE v.CODVARIAVEL = @codVariavel", new { codVariavel });
    }

    private static async Task<UnidadeRow?> ResolveUnidadeAsync(FbConnection conn, string descricao)
    {
        var rows = (await conn.QueryAsync<UnidadeRow>(@"
            SELECT CODUNIDADEMEDIDA AS CodUnidadeMedida, DESCRICAO AS Descricao
            FROM UNIDADEMEDIDA")).AsList();
        var target = UnidadeConversaoCatalog.NormalizeDescricao(descricao);
        return rows.FirstOrDefault(r =>
            UnidadeConversaoCatalog.NormalizeDescricao(r.Descricao) == target);
    }

    private static async Task<List<FaixaRow>> LoadFaixasAsync(FbConnection conn, ConversaoUnidadeRequest req)
    {
        if (req.CodPadrao is > 0)
        {
            return (await conn.QueryAsync<FaixaRow>(@"
                SELECT CODFAIXA AS Id, 'padrao' AS Tipo, SEXO AS Sexo,
                       VALORMIN AS ValorMin, VALORMAX AS ValorMax,
                       IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax
                FROM PADRAONORMALIDADEFAIXA
                WHERE CODPADRAO = @codPadrao AND CODVARIAVEL = @codVariavel
                ORDER BY IDADE_MIN NULLS FIRST, SEXO, VALORMIN",
                new { codPadrao = req.CodPadrao, codVariavel = req.CodVariavel })).AsList();
        }

        var sql = @"
            SELECT CODNORMALIDADE AS Id, 'referencia' AS Tipo, SEXO AS Sexo,
                   VALORMIN AS ValorMin, VALORMAX AS ValorMax,
                   IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax
            FROM NORMALIDADE
            WHERE CODVARIAVEL = @codVariavel";
        if (req.CodReferencia is > 0)
            sql += " AND CODREFERENCIA = @codReferencia";
        sql += " ORDER BY CODREFERENCIA, IDADE_MIN NULLS FIRST, SEXO, VALORMIN";
        return (await conn.QueryAsync<FaixaRow>(sql,
            new { codVariavel = req.CodVariavel, codReferencia = req.CodReferencia })).AsList();
    }

    private static async Task<List<ComentarioRow>> LoadComentariosAsync(FbConnection conn, ConversaoUnidadeRequest req)
    {
        if (req.CodPadrao is > 0)
        {
            return (await conn.QueryAsync<ComentarioRow>(@"
                SELECT CODPADRAOCOMENTARIO AS Id, 'padrao' AS Tipo, SEXO AS Sexo,
                       IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax, TEXTO AS Texto
                FROM PADRAONORMALIDADECOMENTARIO
                WHERE CODPADRAO = @codPadrao AND CODVARIAVEL = @codVariavel
                ORDER BY IDADE_MIN, SEXO",
                new { codPadrao = req.CodPadrao, codVariavel = req.CodVariavel })).AsList();
        }

        var sql = @"
            SELECT CODNORMALIDADECOMENTARIO AS Id, 'referencia' AS Tipo, SEXO AS Sexo,
                   IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax, TEXTO AS Texto
            FROM NORMALIDADECOMENTARIO
            WHERE CODVARIAVEL = @codVariavel";
        if (req.CodReferencia is > 0)
            sql += " AND CODREFERENCIA = @codReferencia";
        sql += " ORDER BY CODREFERENCIA, IDADE_MIN, SEXO";
        return (await conn.QueryAsync<ComentarioRow>(sql,
            new { codVariavel = req.CodVariavel, codReferencia = req.CodReferencia })).AsList();
    }

    private sealed class VariavelUnidadeRow
    {
        public int CodVariavel { get; set; }
        public string Nome { get; set; } = "";
        public string? Variavel { get; set; }
        public int? CodUnidadeMedida { get; set; }
        public int CasasDecimais { get; set; }
        public string? UnidadeDescricao { get; set; }
    }

    private sealed class UnidadeRow
    {
        public int CodUnidadeMedida { get; set; }
        public string Descricao { get; set; } = "";
    }

    private sealed class FaixaRow
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "referencia";
        public string? Sexo { get; set; }
        public decimal? ValorMin { get; set; }
        public decimal? ValorMax { get; set; }
        public int? IdadeMin { get; set; }
        public int? IdadeMax { get; set; }
    }

    private sealed class ComentarioRow
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "referencia";
        public string? Sexo { get; set; }
        public int IdadeMin { get; set; }
        public int IdadeMax { get; set; }
        public string Texto { get; set; } = "";
    }
}

public sealed class ConversaoUnidadeRequest
{
    public int CodVariavel { get; set; }
    public int? CodReferencia { get; set; }
    public int? CodPadrao { get; set; }
    /// <summary>Opcional; se omitido, usa a unidade atual da variável.</summary>
    public string? UnidadeOrigem { get; set; }
    public string UnidadeDestino { get; set; } = "";
    public int? CasasDecimais { get; set; }
}

public sealed class ConversaoUnidadeApplyRequest
{
    public ConversaoUnidadeRequest Preview { get; set; } = new();
    public List<int>? FaixaIds { get; set; }
    public List<int>? ComentarioIds { get; set; }
    public bool? AtualizarUnidadeVariavel { get; set; } = true;
}

public sealed class ConversaoUnidadePreviewDto
{
    public int CodVariavel { get; set; }
    public string NomeVariavel { get; set; } = "";
    public string? CodigoVariavel { get; set; }
    public string UnidadeOrigem { get; set; } = "";
    public string UnidadeDestino { get; set; } = "";
    public int CodUnidadeDestino { get; set; }
    public int? CodUnidadeOrigemAtual { get; set; }
    public decimal Fator { get; set; }
    public int CasasDecimais { get; set; }
    public bool AtualizarUnidadeVariavel { get; set; }
    public List<ConversaoFaixaPreviewDto> Faixas { get; set; } = [];
    public List<ConversaoComentarioPreviewDto> Comentarios { get; set; } = [];
    public List<string> NaoConvertidos { get; set; } = [];
    public ConversaoUnidadeResumoDto Resumo { get; set; } = new();
}

public sealed class ConversaoUnidadeResumoDto
{
    public int TotalFaixas { get; set; }
    public int FaixasIncluidasDefault { get; set; }
    public int FaixasJaDestino { get; set; }
    public int TotalComentarios { get; set; }
    public int ComentariosIncluidosDefault { get; set; }
    public int ComentariosNaoConvertiveis { get; set; }
}

public sealed class ConversaoFaixaPreviewDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "referencia";
    public string? Sexo { get; set; }
    public int? IdadeMin { get; set; }
    public int? IdadeMax { get; set; }
    public decimal? ValorMinAtual { get; set; }
    public decimal? ValorMaxAtual { get; set; }
    public decimal? ValorMinNovo { get; set; }
    public decimal? ValorMaxNovo { get; set; }
    public string Classificacao { get; set; } = "ambigua";
    public bool IncluirDefault { get; set; }
}

public sealed class ConversaoComentarioPreviewDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "referencia";
    public string? Sexo { get; set; }
    public int IdadeMin { get; set; }
    public int IdadeMax { get; set; }
    public string TextoAtual { get; set; } = "";
    public string? TextoNovo { get; set; }
    public bool Convertivel { get; set; }
    public string? Motivo { get; set; }
    public string Classificacao { get; set; } = "ambigua";
    public bool IncluirDefault { get; set; }
}

public sealed class ConversaoUnidadeApplyResultDto
{
    public int FaixasAtualizadas { get; set; }
    public int ComentariosAtualizados { get; set; }
    public bool UnidadeVariavelAtualizada { get; set; }
    public int CodUnidadeDestino { get; set; }
    public string UnidadeDestino { get; set; } = "";
    public string Message { get; set; } = "";
}
