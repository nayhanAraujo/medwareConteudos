using System.IO.Compression;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.ApiPublica;

public class ApiScriptOperations
{
    private readonly IFirebirdConnectionFactory _db;
    private readonly string _migrationRoot;
    private readonly string _staticRoot;
    private readonly string _uploadRoot;
    private readonly string? _legacyRoot;

    private const string ScriptListSelect = @"
        SELECT s.CODSCRIPTLAUDO, s.NOME, s.DESCRICAO, s.LINGUAGEM, s.SISTEMA,
               s.APROVADO, s.DATA_VERIFICACAO, s.ATIVO, s.APROVADO_POR,
               p.NOME AS NOME_PACOTE, s.CODPACOTE
        FROM SCRIPTLAUDO s
        LEFT JOIN PACOTES p ON s.CODPACOTE = p.CODPACOTE";

    public ApiScriptOperations(
        IFirebirdConnectionFactory db,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _db = db;
        _migrationRoot = MigrationRootResolver.ResolveMigrationRoot(config, env.ContentRootPath);
        _staticRoot = MigrationRootResolver.StaticRoot(_migrationRoot);
        _uploadRoot = MigrationRootResolver.UploadsRoot(_migrationRoot, create: false);
        _legacyRoot = MigrationRootResolver.ResolveLegacyRoot(config, _migrationRoot, env.ContentRootPath);
    }

    public static bool ParseIncluirArquivos(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v is not ("0" or "false" or "no" or "nao" or "não");
    }

    public static (List<string> Where, DynamicParameters Params) BuildWhere(
        string? sistema, string? aprovado, string? ativo, string? pacote, IEnumerable<string>? extra = null)
    {
        var where = extra?.ToList() ?? new List<string>();
        var p = new DynamicParameters();
        if (sistema is "Laudos UX" or "Laudos Flex") { where.Add("s.SISTEMA = @sistema"); p.Add("sistema", sistema); }
        if (aprovado is "0" or "1") { where.Add("s.APROVADO = @aprovado"); p.Add("aprovado", int.Parse(aprovado)); }
        if (ativo is "0" or "1") { where.Add("s.ATIVO = @ativo"); p.Add("ativo", int.Parse(ativo)); }
        if (int.TryParse(pacote, out var codPac)) { where.Add("s.CODPACOTE = @pacote"); p.Add("pacote", codPac); }
        if (where.Count == 0) where.Add("1=1");
        return (where, p);
    }

    public async Task<List<Dictionary<string, object?>>> ListScriptsAsync(
        string? sistema, string? aprovado, string? ativo, string? pacote, bool incluirArquivos, CancellationToken ct)
    {
        var (where, p) = BuildWhere(sistema, aprovado, ativo, pacote);
        var sql = $"{ScriptListSelect} WHERE {string.Join(" AND ", where)} ORDER BY s.NOME";
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync(sql, p)).ToList();
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
            list.Add(await MapScriptRowAsync(conn, row, incluirArquivos, ct));
        return list;
    }

    public async Task<(Dictionary<string, object?>? Item, string? WorkflowKey)> GetUltimoVerificadoAsync(
        string? sistema, string? aprovado, string? ativo, string? pacote, bool incluirArquivos, CancellationToken ct)
    {
        var (where, p) = BuildWhere(sistema, aprovado, ativo, pacote, new[] { "s.DATA_VERIFICACAO IS NOT NULL" });
        var sql = $@"
            SELECT FIRST 1
            s.CODSCRIPTLAUDO, s.NOME, s.DESCRICAO, s.LINGUAGEM, s.SISTEMA,
            s.APROVADO, s.DATA_VERIFICACAO, s.ATIVO, s.APROVADO_POR,
            p.NOME AS NOME_PACOTE, s.CODPACOTE
            FROM SCRIPTLAUDO s
            LEFT JOIN PACOTES p ON s.CODPACOTE = p.CODPACOTE
            WHERE {string.Join(" AND ", where)}
            ORDER BY s.DATA_VERIFICACAO DESC";
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(sql, p);
        if (row is null) return (null, null);

        var item = await MapScriptRowAsync(conn, row, incluirArquivos, ct);
        var imgList = item.TryGetValue("imagens", out object? imgsObj) ? imgsObj as List<Dictionary<string, string>> : null;
        if (imgList is { Count: > 0 })
        {
            var cod = (int)item["codscriptlaudo"]!;
            imgList[0].TryGetValue("nome", out var imgNome);
            item["imagem_capa"] = new Dictionary<string, object?>
            {
                ["indice"] = 0,
                ["nome"] = imgNome,
                ["caminho_relativo_api"] = $"/apiconteudos/v1/scripts/{cod}/imagem?indice=0"
            };
        }
        return (item, BuildWorkflowKey(item));
    }

    public async Task<(string? Path, Dictionary<string, string>? Meta)> ResolveScriptImagemAsync(
        int codscriptlaudo, int indice, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int?>(
            "SELECT 1 FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @id", new { id = codscriptlaudo });
        if (exists != 1) return (null, null);

        var (codVersao, numeroVersao) = await GetVersaoAtivaAsync(conn, codscriptlaudo, ct);
        var (imagens, _, _, _) = await FetchArquivosAsync(conn, codscriptlaudo, codVersao, numeroVersao, ct);
        if (indice < 0 || indice >= imagens.Count) return (null, imagens.ElementAtOrDefault(indice));

        var meta = imagens[indice];
        meta.TryGetValue("caminho", out var caminhoMeta);
        var path = ResolveArquivoCaminhoDisco(caminhoMeta);
        return (path, meta);
    }

    public async Task<IActionResult> DownloadScriptAsync(int codscriptlaudo, string? tipo, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT NOME, SISTEMA, LINGUAGEM, ARQUIVO_JSON, DLL
            FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO = @id", new { id = codscriptlaudo });
        if (row is null)
            return new NotFoundObjectResult(new { success = false, error = "Script não encontrado", codscriptlaudo });

        var nomeScript = (string?)row.NOME;
        var sistema = (string?)row.SISTEMA ?? "";
        var linguagem = (string?)row.LINGUAGEM;
        var jsonBytes = BlobHelper.ToBytes((object?)row.ARQUIVO_JSON);
        var dllBytes = BlobHelper.ToBytes((object?)row.DLL);

        var merged = await MergeActiveVersionBlobsAsync(conn, codscriptlaudo, jsonBytes, null, dllBytes, ct);
        var jsonBytesMerged = merged.Json;
        var mrdBytes = merged.Mrd;
        var dllBytesMerged = merged.Dll;
        var numeroVersao = merged.NumeroVersao;
        var activeVersion = merged.ActiveVersion;
        var codVersaoAtivo = merged.CodVersao;

        var mrdListAll = await ListMrdForDownloadAsync(conn, codscriptlaudo, activeVersion ? codVersaoAtivo : null, ct);
        var multiMrd = mrdListAll.Count > 1;
        var saneNome = SanitizeFileName(nomeScript, codscriptlaudo);
        var fileBase = saneNome;
        var mrdExt = MrdExtension(linguagem);
        tipo = (tipo ?? "").Trim().ToLowerInvariant();

        if (tipo == "json")
        {
            if (jsonBytesMerged is null || jsonBytesMerged.Length == 0)
                return NotFoundMsg("Arquivo JSON não disponível para este script", codscriptlaudo);
            if (sistema != "Laudos UX")
                return BadMsg("Arquivo JSON disponível apenas para scripts Laudos UX", codscriptlaudo);
            return ZipResponse(new[] { ($"{fileBase}_script.json", jsonBytesMerged) }, saneNome, numeroVersao, activeVersion);
        }

        if (tipo == "dll")
        {
            if (dllBytesMerged is null || dllBytesMerged.Length == 0)
                return NotFoundMsg("Arquivo DLL não disponível para este script", codscriptlaudo);
            if (sistema != "Laudos Flex")
                return BadMsg("DLL disponível apenas para scripts Laudos Flex", codscriptlaudo);
            return ZipResponse(new[] { ($"{fileBase}.dll", dllBytesMerged) }, saneNome, numeroVersao, activeVersion);
        }

        if (tipo == "mrd")
        {
            if (mrdListAll.Count == 0 && (mrdBytes is null || mrdBytes.Length == 0))
                return NotFoundMsg("Arquivo MRD não disponível para este script", codscriptlaudo);
            List<(string, byte[])> entries;
            if (mrdListAll.Count > 0)
            {
                var padraoOnly = mrdListAll.Where(m => m.Padrao).ToList();
                if (padraoOnly.Count == 0) padraoOnly.Add(mrdListAll[0]);
                entries = MrdZipEntries(padraoOnly, fileBase, mrdExt, sistema, multiMrd);
            }
            else
            {
                var mrdNameSuffix = sistema == "Laudos UX" ? "_mrd" : "";
                entries = new List<(string, byte[])> { ($"{fileBase}{mrdNameSuffix}.{mrdExt}", mrdBytes!) };
            }
            return ZipResponse(entries, saneNome, numeroVersao, activeVersion);
        }

        if (tipo == "mrd_todos")
        {
            if (mrdListAll.Count == 0)
                return NotFoundMsg("Nenhum arquivo MRD disponível para este script", codscriptlaudo);
            return ZipResponse(MrdZipEntries(mrdListAll, fileBase, mrdExt, sistema), $"{saneNome}_mrd_todos", numeroVersao, activeVersion);
        }

        if (sistema == "Laudos UX")
        {
            if (jsonBytesMerged is null || jsonBytesMerged.Length == 0)
                return NotFoundMsg("Arquivo JSON obrigatório para scripts Laudos UX", codscriptlaudo);
            var files = new List<(string, byte[])> { ($"{fileBase}_script.json", jsonBytesMerged) };
            if (mrdListAll.Count > 0) files.AddRange(MrdZipEntries(mrdListAll, fileBase, mrdExt, sistema));
            else if (mrdBytes is not null && mrdBytes.Length > 0) files.Add(($"{fileBase}_mrd.{mrdExt}", mrdBytes));
            return ZipResponse(files, saneNome, numeroVersao, activeVersion);
        }

        if (sistema == "Laudos Flex")
        {
            if (dllBytesMerged is null || dllBytesMerged.Length == 0)
                return NotFoundMsg("Arquivo DLL obrigatório para scripts Laudos Flex", codscriptlaudo);
            var files = new List<(string, byte[])> { ($"{fileBase}.dll", dllBytesMerged) };
            if (mrdListAll.Count > 0) files.AddRange(MrdZipEntries(mrdListAll, fileBase, mrdExt, sistema));
            else if (mrdBytes is not null && mrdBytes.Length > 0) files.Add(($"{fileBase}.{mrdExt}", mrdBytes));
            return ZipResponse(files, saneNome, numeroVersao, activeVersion);
        }

        return new BadRequestObjectResult(new
        {
            success = false,
            error = "Sistema do script não reconhecido (esperado Laudos UX ou Laudos Flex)",
            codscriptlaudo,
            sistema
        });
    }

    private async Task<Dictionary<string, object?>> MapScriptRowAsync(
        System.Data.IDbConnection conn, dynamic row, bool incluirArquivos, CancellationToken ct)
    {
        var cod = (int)row.CODSCRIPTLAUDO;
        var item = new Dictionary<string, object?>
        {
            ["codscriptlaudo"] = cod,
            ["nome"] = (string?)row.NOME,
            ["descricao"] = (string?)row.DESCRICAO ?? "",
            ["linguagem"] = (string?)row.LINGUAGEM ?? "",
            ["sistema"] = (string?)row.SISTEMA ?? "",
            ["aprovado"] = Convert.ToBoolean(row.APROVADO),
            ["data_verificacao"] = FormatDate(row.DATA_VERIFICACAO),
            ["ativo"] = Convert.ToBoolean(row.ATIVO),
            ["aprovado_por"] = (string?)row.APROVADO_POR ?? "",
            ["pacote_nome"] = (string?)row.NOME_PACOTE ?? "",
            ["codpacote"] = row.CODPACOTE
        };

        if (!incluirArquivos) return item;

        var (codVersao, numeroVersao) = await GetVersaoAtivaAsync(conn, cod, ct);
        var (imagens, pdfs, numVersao, codVers) = await FetchArquivosAsync(conn, cod, codVersao, numeroVersao, ct);
        var (mrds, mrdFonte) = await FetchMrdListAsync(conn, cod, codVers, ct);
        item["numero_versao"] = numVersao;
        item["imagens"] = imagens;
        item["pdfs"] = pdfs;
        item["mrds"] = mrds;
        item["qtd_mrd"] = mrds.Count;
        item["mrd_fonte"] = mrdFonte;
        return item;
    }

    private static string? FormatDate(object? value)
    {
        if (value is null or DBNull) return null;
        if (value is DateTime dt) return dt.ToString("o");
        return value.ToString();
    }

    private static string? BuildWorkflowKey(Dictionary<string, object?> item)
    {
        if (!item.TryGetValue("codscriptlaudo", out var cod) || cod is null) return null;
        if (!item.TryGetValue("data_verificacao", out var dv) || dv is null or "") return null;
        return $"{cod}|{dv}";
    }

    private static async Task<(int? CodVersao, string? NumeroVersao)> GetVersaoAtivaAsync(
        System.Data.IDbConnection conn, int codscriptlaudo, CancellationToken ct)
    {
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODVERSAO, NUMERO_VERSAO FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @id AND ATIVO = 'T'
            ORDER BY DATA_CRIACAO DESC ROWS 1", new { id = codscriptlaudo });
        return row is null ? (null, null) : ((int?)row.CODVERSAO, (string?)row.NUMERO_VERSAO);
    }

    private static async Task<(List<Dictionary<string, string>> Imagens, List<Dictionary<string, string>> Pdfs, string? NumeroVersao, int? CodVersao)>
        FetchArquivosAsync(System.Data.IDbConnection conn, int codscriptlaudo, int? codVersao, string? numeroVersao, CancellationToken ct)
    {
        var imagensPadrao = new List<Dictionary<string, string>>();
        var pdfsPadrao = new List<Dictionary<string, string>>();
        var rows = await conn.QueryAsync(@"
            SELECT TIPO, CAMINHO, NOME_ARQUIVO FROM SCRIPTARQUIVOS
            WHERE CODSCRIPTLAUDO = @id ORDER BY NOME_ARQUIVO", new { id = codscriptlaudo });
        foreach (var r in rows)
        {
            var item = new Dictionary<string, string> { ["nome"] = (string?)r.NOME_ARQUIVO ?? "", ["caminho"] = (string?)r.CAMINHO ?? "" };
            if ((string?)r.TIPO == "IMAGEM") imagensPadrao.Add(item);
            else if ((string?)r.TIPO == "PDF") pdfsPadrao.Add(item);
        }

        var imagens = imagensPadrao;
        var pdfs = pdfsPadrao;
        if (codVersao.HasValue)
        {
            var imagensVersao = new List<Dictionary<string, string>>();
            var pdfsVersao = new List<Dictionary<string, string>>();
            var vrows = await conn.QueryAsync(@"
                SELECT TIPO, CAMINHO, NOME_ARQUIVO FROM SCRIPTVERSAOARQUIVOS
                WHERE CODVERSAO = @id ORDER BY DATA_UPLOAD DESC", new { id = codVersao.Value });
            foreach (var r in vrows)
            {
                var item = new Dictionary<string, string> { ["nome"] = (string?)r.NOME_ARQUIVO ?? "", ["caminho"] = (string?)r.CAMINHO ?? "" };
                if ((string?)r.TIPO == "IMAGEM") imagensVersao.Add(item);
                else if ((string?)r.TIPO == "PDF") pdfsVersao.Add(item);
            }
            if (imagensVersao.Count > 0) imagens = imagensVersao;
            if (pdfsVersao.Count > 0) pdfs = pdfsVersao;
        }
        return (imagens, pdfs, numeroVersao, codVersao);
    }

    private static async Task<(List<Dictionary<string, object?>> Mrds, string Fonte)> FetchMrdListAsync(
        System.Data.IDbConnection conn, int codscriptlaudo, int? codVersao, CancellationToken ct)
    {
        if (codVersao.HasValue)
        {
            var rows = await conn.QueryAsync(@"
                SELECT CODVERSAOMRD, NOME_ARQUIVO, PADRAO, ORDEM FROM SCRIPTVERSAOMRD
                WHERE CODVERSAO = @id
                ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, ORDEM, CODVERSAOMRD", new { id = codVersao.Value });
            return (rows.Select(r => new Dictionary<string, object?>
            {
                ["codversaomrd"] = (int)r.CODVERSAOMRD,
                ["codscriptmrd"] = null,
                ["nome_arquivo"] = (string?)r.NOME_ARQUIVO ?? $"MRD_{r.CODVERSAOMRD}",
                ["padrao"] = ((string?)r.PADRAO ?? "F").Trim().ToUpperInvariant() == "T",
                ["ordem"] = r.ORDEM
            }).ToList(), "SCRIPTVERSAOMRD");
        }

        var srows = await conn.QueryAsync(@"
            SELECT CODSCRIPTMRD, NOME_ARQUIVO, PADRAO, ORDEM FROM SCRIPTLAUDOMRD
            WHERE CODSCRIPTLAUDO = @id
            ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, ORDEM, CODSCRIPTMRD", new { id = codscriptlaudo });
        return (srows.Select(r => new Dictionary<string, object?>
        {
            ["codscriptmrd"] = (int)r.CODSCRIPTMRD,
            ["codversaomrd"] = null,
            ["nome_arquivo"] = (string?)r.NOME_ARQUIVO ?? $"MRD_{r.CODSCRIPTMRD}",
            ["padrao"] = ((string?)r.PADRAO ?? "F").Trim().ToUpperInvariant() == "T",
            ["ordem"] = r.ORDEM
        }).ToList(), "SCRIPTLAUDOMRD");
    }

    private string? ResolveArquivoCaminhoDisco(string? caminhoBruto)
    {
        var caminho = (caminhoBruto ?? "").Trim();
        if (string.IsNullOrEmpty(caminho)) return null;
        if (File.Exists(caminho)) return Path.GetFullPath(caminho);

        var rel = caminho.Replace('\\', '/').TrimStart('/');
        var candidatos = new List<string>();
        if (rel.StartsWith("static/", StringComparison.OrdinalIgnoreCase))
        {
            candidatos.Add(Path.Combine(_migrationRoot, rel));
            rel = rel["static/".Length..];
        }

        candidatos.Add(Path.Combine(_migrationRoot, rel));
        candidatos.Add(Path.Combine(_staticRoot, rel));
        candidatos.Add(Path.Combine(_uploadRoot, rel));

        if (rel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var uploadRelative = rel["uploads/".Length..];
            candidatos.Add(Path.Combine(_uploadRoot, uploadRelative));
            candidatos.Add(Path.Combine(_staticRoot, "uploads", uploadRelative));
        }

        if (_legacyRoot is not null)
        {
            candidatos.Add(Path.Combine(_legacyRoot, rel));
            candidatos.Add(Path.Combine(_legacyRoot, "static", rel));
            candidatos.Add(Path.Combine(_legacyRoot, "uploads", rel));
        }

        foreach (var path in candidatos.Distinct(StringComparer.OrdinalIgnoreCase))
            if (File.Exists(path)) return Path.GetFullPath(path);
        return null;
    }

    private static async Task<(byte[]? Json, byte[]? Mrd, byte[]? Dll, string? NumeroVersao, bool ActiveVersion, int? CodVersao)>
        MergeActiveVersionBlobsAsync(System.Data.IDbConnection conn, int codscriptlaudo, byte[]? jsonBytes, byte[]? mrdBytes, byte[]? dllBytes, CancellationToken ct)
    {
        var v = await conn.QueryFirstOrDefaultAsync(@"
            SELECT CODVERSAO, NUMERO_VERSAO, ARQUIVO_JSON, ARQUIVO_DLL FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @id AND ATIVO = 'T' ORDER BY DATA_CRIACAO DESC ROWS 1", new { id = codscriptlaudo });
        if (v is null)
        {
            var mrdPadrao = await GetScriptMrdPadraoBytesAsync(conn, codscriptlaudo, ct);
            if (mrdPadrao is not null && mrdPadrao.Length > 0) mrdBytes = mrdPadrao;
            return (jsonBytes, mrdBytes, dllBytes, null, false, null);
        }

        var codversao = (int)v.CODVERSAO;
        var jb = BlobHelper.ToBytes((object?)v.ARQUIVO_JSON);
        if (jb is not null && jb.Length > 0) jsonBytes = jb;
        var mrdVersao = await GetVersaoMrdPadraoBytesAsync(conn, codversao, ct);
        if (mrdVersao is not null && mrdVersao.Length > 0) mrdBytes = mrdVersao;
        else if (mrdBytes is null || mrdBytes.Length == 0) mrdBytes = await GetScriptMrdPadraoBytesAsync(conn, codscriptlaudo, ct);
        var db = BlobHelper.ToBytes((object?)v.ARQUIVO_DLL);
        if (db is not null && db.Length > 0) dllBytes = db;
        return (jsonBytes, mrdBytes, dllBytes, (string?)v.NUMERO_VERSAO, true, codversao);
    }

    private static async Task<byte[]?> GetScriptMrdPadraoBytesAsync(System.Data.IDbConnection conn, int codscriptlaudo, CancellationToken ct)
    {
        var blob = await conn.ExecuteScalarAsync<object?>(@"
            SELECT ARQUIVO_MRD FROM SCRIPTLAUDOMRD
            WHERE CODSCRIPTLAUDO = @id AND PADRAO = 'T' ORDER BY CODSCRIPTMRD ROWS 1", new { id = codscriptlaudo });
        return BlobHelper.ToBytes(blob);
    }

    private static async Task<byte[]?> GetVersaoMrdPadraoBytesAsync(System.Data.IDbConnection conn, int codversao, CancellationToken ct)
    {
        var blob = await conn.ExecuteScalarAsync<object?>(@"
            SELECT ARQUIVO_MRD FROM SCRIPTVERSAOMRD
            WHERE CODVERSAO = @id AND PADRAO = 'T' ORDER BY CODVERSAOMRD ROWS 1", new { id = codversao });
        return BlobHelper.ToBytes(blob);
    }

    private sealed class MrdDownloadItem
    {
        public string NomeArquivo { get; init; } = "";
        public byte[] Bytes { get; init; } = Array.Empty<byte>();
        public bool Padrao { get; init; }
    }

    private static async Task<List<MrdDownloadItem>> ListMrdForDownloadAsync(
        System.Data.IDbConnection conn, int codscriptlaudo, int? codversao, CancellationToken ct)
    {
        var result = new List<MrdDownloadItem>();
        IEnumerable<dynamic> rows;
        if (codversao.HasValue)
        {
            rows = await conn.QueryAsync(@"
                SELECT NOME_ARQUIVO, ARQUIVO_MRD, PADRAO FROM SCRIPTVERSAOMRD
                WHERE CODVERSAO = @id ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, CODVERSAOMRD", new { id = codversao.Value });
        }
        else
        {
            rows = await conn.QueryAsync(@"
                SELECT NOME_ARQUIVO, ARQUIVO_MRD, PADRAO FROM SCRIPTLAUDOMRD
                WHERE CODSCRIPTLAUDO = @id ORDER BY CASE WHEN PADRAO = 'T' THEN 0 ELSE 1 END, CODSCRIPTMRD", new { id = codscriptlaudo });
        }

        foreach (var r in rows)
        {
            byte[]? b = BlobHelper.ToBytes((object?)r.ARQUIVO_MRD);
            if (b is not null && b.Length > 0)
                result.Add(new MrdDownloadItem
                {
                    NomeArquivo = (string?)r.NOME_ARQUIVO ?? "mrd",
                    Bytes = b,
                    Padrao = ((string?)r.PADRAO ?? "F").Trim().ToUpperInvariant() == "T"
                });
        }
        return result;
    }

    private static string MrdExtension(string? linguagem) =>
        !string.IsNullOrEmpty(linguagem) && linguagem.Contains("HTML", StringComparison.OrdinalIgnoreCase) ? "json" : "mrd";

    private static List<(string Name, byte[] Data)> MrdZipEntries(
        List<MrdDownloadItem> mrdList, string fileBase, string mrdExt, string sistema, bool? multiOverride = null)
    {
        var multi = multiOverride ?? mrdList.Count > 1;
        var entries = new List<(string, byte[])>();
        foreach (var item in mrdList)
        {
            string arc;
            if (sistema == "Laudos UX")
            {
                if (multi && !item.Padrao)
                {
                    var stem = MrdStemFromNome(item.NomeArquivo, fileBase);
                    if (!stem.EndsWith("_mrd", StringComparison.OrdinalIgnoreCase)) stem += "_mrd";
                    arc = $"{stem}.{mrdExt}";
                }
                else arc = $"{fileBase}_mrd.{mrdExt}";
            }
            else
            {
                arc = multi && !item.Padrao
                    ? $"{MrdStemFromNome(item.NomeArquivo, fileBase)}.{mrdExt}"
                    : $"{fileBase}.{mrdExt}";
            }
            entries.Add((arc, item.Bytes));
        }
        return entries;
    }

    private static string MrdStemFromNome(string? nomeArquivo, string fileBase)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo)) return fileBase;
        var s = nomeArquivo.Trim();
        var dot = s.LastIndexOf('.');
        if (dot > 0) s = s[..dot];
        return string.IsNullOrWhiteSpace(s) ? fileBase : s.Trim();
    }

    private static string SanitizeFileName(string? nome, int cod)
    {
        var sane = string.Concat((nome ?? "").Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' ? c : '_')).Trim();
        sane = HttpHeaderSanitizer.ToAscii(sane).Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sane) ? $"script_{cod}" : sane;
    }

    private static IActionResult ZipResponse(IEnumerable<(string Name, byte[] Data)> files, string zipBaseName, string? numeroVersao, bool activeVersion)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (name, data) in files)
            {
                var entry = zip.CreateEntry(name);
                using var s = entry.Open();
                s.Write(data, 0, data.Length);
            }
        }
        return new ScriptDownloadFileResult(ms.ToArray(), $"{zipBaseName}.zip", numeroVersao, activeVersion);
    }

    private static IActionResult NotFoundMsg(string error, int cod) =>
        new NotFoundObjectResult(new { success = false, error, codscriptlaudo = cod });

    private static IActionResult BadMsg(string error, int cod) =>
        new BadRequestObjectResult(new { success = false, error, codscriptlaudo = cod });
}

public class ScriptDownloadFileResult : FileContentResult
{
    public ScriptDownloadFileResult(byte[] bytes, string fileName, string? numeroVersao, bool activeVersion)
        : base(bytes, "application/zip")
    {
        FileDownloadName = HttpHeaderSanitizer.ToAscii(fileName);
        Suffix = BuildSuffix(numeroVersao, activeVersion);
        Source = activeVersion ? "SCRIPTVERSOES" : "SCRIPTLAUDO";
    }

    public string Suffix { get; }
    public string Source { get; }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.Headers["X-Script-Download-Suffix"] = HttpHeaderSanitizer.ToAscii(Suffix);
        context.HttpContext.Response.Headers["X-Script-Download-Source"] = Source;
        return base.ExecuteResultAsync(context);
    }

    private static string BuildSuffix(string? numeroVersao, bool activeVersion)
    {
        if (!activeVersion) return "padrão";
        if (string.IsNullOrWhiteSpace(numeroVersao)) return "ativa";
        var safe = string.Concat(numeroVersao.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.' ? c : '_')).Trim().Replace(' ', '_');
        return string.IsNullOrWhiteSpace(safe) ? "ativa" : safe;
    }
}
