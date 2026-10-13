using System.IO.Compression;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace MdwConteudos.Api.Modules.Web;

public partial class ScriptsService
{
    private static string JsonMrdFilename(string name) =>
        Path.ChangeExtension(SafeExportName(name, "arquivo"), ".json");
    private sealed record ExportMrd(int Id, string Name, byte[] Content);
    private sealed record ScriptExport(int Id, string Name, string System, byte[]? Json, List<ExportMrd> Mrds);

    private async Task<ScriptExport?> ResolveScriptExportAsync(FbConnection conn, int id, bool includeMrds)
    {
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT s.NOME, s.SISTEMA, s.ARQUIVO_JSON,
                (SELECT FIRST 1 v.CODVERSAO FROM SCRIPTVERSOES v
                 WHERE v.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO AND v.ATIVO = 'T'
                 ORDER BY v.DATA_CRIACAO DESC) AS CODVERSAO
            FROM SCRIPTLAUDO s WHERE s.CODSCRIPTLAUDO = @id", new { id });
        if (row is null) return null;
        var data = (IDictionary<string, object>)row;
        var json = BlobToBytes(data["ARQUIVO_JSON"]);
        int? version = data["CODVERSAO"] is null or DBNull ? null : Convert.ToInt32(data["CODVERSAO"]);
        var mrds = new List<ExportMrd>();
        if (version.HasValue)
        {
            var versionJson = await conn.ExecuteScalarAsync<object>(
                "SELECT ARQUIVO_JSON FROM SCRIPTVERSOES WHERE CODVERSAO = @version", new { version });
            json = BlobToBytes(versionJson) ?? json;
            if (includeMrds)
                mrds = await ReadExportMrdsAsync(conn, true, version.Value);
        }
        if (includeMrds && mrds.Count == 0)
            mrds = await ReadExportMrdsAsync(conn, false, id);
        return new(id, data["NOME"]?.ToString() ?? "script", data["SISTEMA"]?.ToString() ?? "", json, mrds);
    }

    private static async Task<List<ExportMrd>> ReadExportMrdsAsync(FbConnection conn, bool version, int id)
    {
        var sql = version
            ? "SELECT CODVERSAOMRD AS ID, NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTVERSAOMRD WHERE CODVERSAO = @id ORDER BY CODVERSAOMRD"
            : "SELECT CODSCRIPTMRD AS ID, NOME_ARQUIVO, ARQUIVO_MRD FROM SCRIPTLAUDOMRD WHERE CODSCRIPTLAUDO = @id ORDER BY CODSCRIPTMRD";
        var files = new List<ExportMrd>();
        foreach (var row in await conn.QueryAsync(sql, new { id }))
        {
            var data = (IDictionary<string, object>)row;
            var bytes = BlobToBytes(data["ARQUIVO_MRD"]);
            if (bytes is not null)
                files.Add(new(Convert.ToInt32(data["ID"]), data["NOME_ARQUIVO"]?.ToString() ?? "arquivo.mrd", bytes));
        }
        return files;
    }

    public async Task<(byte[] Content, string Filename, string Mime)?> ExportScriptPackageAsync(int id)
    {
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var script = await ResolveScriptExportAsync(conn, id, true);
        if (script is null) return null;
        ValidateScriptExport(script);
        var name = SafeExportName(script.Name, "script");
        if (script.Mrds.Count == 0) return (script.Json!, name + ".json", "application/json");
        return (await BuildScriptZipAsync([script]), $"{name}_{id}.zip", "application/zip");
    }

    public async Task<byte[]> ExportScriptBatchAsync(int[]? ids)
    {
        if (ids is null || ids.Length == 0) throw new InvalidOperationException("Selecione ao menos um modelo.");
        await using var conn = (FbConnection)CreateConnection();
        await conn.OpenAsync();
        var scripts = new List<ScriptExport>();
        foreach (var id in ids.Distinct())
        {
            var script = await ResolveScriptExportAsync(conn, id, true);
            if (script is null) throw new InvalidOperationException($"Modelo {id} nao encontrado.");
            ValidateScriptExport(script);
            scripts.Add(script);
        }
        return await BuildScriptZipAsync(scripts);
    }

    private static void ValidateScriptExport(ScriptExport script)
    {
        if (script.System != "Laudos UX" || script.Json is null)
            throw new InvalidOperationException($"Modelo {script.Id} precisa ser Laudos UX e possuir JSON.");
    }

    private static string SafeExportName(string name, string fallback)
    {
        var safe = string.Concat(name.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..") return fallback;
        return safe;
    }

    private static async Task<byte[]> BuildScriptZipAsync(IEnumerable<ScriptExport> scripts)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var script in scripts)
            {
                var name = SafeExportName(script.Name, "script");
                var folder = $"{name}_{script.Id}/";
                await WriteZipEntryAsync(zip, folder + name + ".json", script.Json!);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mrd in script.Mrds)
                {
                    var filename = SafeExportName(mrd.Name, $"arquivo_{mrd.Id}.mrd");
                    if (script.System == "Laudos UX") filename = JsonMrdFilename(filename);
                    var original = filename;
                    var suffix = 0;
                    while (!names.Add(filename))
                    {
                        var extension = Path.GetExtension(original);
                        filename = $"{Path.GetFileNameWithoutExtension(original)}_{mrd.Id}{(suffix == 0 ? "" : "_" + suffix)}{extension}";
                        suffix++;
                    }
                    await WriteZipEntryAsync(zip, folder + "mrds/" + filename, mrd.Content);
                }
            }
        }
        return stream.ToArray();
    }

    private static async Task WriteZipEntryAsync(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes);
    }
}
