using System.Data;
using System.IO.Compression;
using System.Text;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Web;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public interface IAssistenteImportacaoService
{
    Task<AssistenteImportacaoResult> ImportAsync(AssistenteImportacaoForm form, CancellationToken ct);
    Task<AssistenteImportacaoLoteResult> ImportManyAsync(AssistenteImportacaoLoteRequest request, CancellationToken ct);
    Task<AssistenteArquivoPreparado?> DownloadScriptAsync(int id, CancellationToken ct);
    Task<AssistenteArquivoPreparado?> DownloadMrdAsync(int id, CancellationToken ct);
    Task<AssistenteArquivoBinario?> ExportScriptPackageAsync(int id, CancellationToken ct);
    Task<AssistenteArquivoBinario?> ExportScriptsPackageAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}

public sealed class AssistenteImportacaoService(
    IAssistantFirebirdConnectionFactory connectionFactory,
    ScriptsService scriptsService,
    Publicacao.PublicacaoService publicacao) : IAssistenteImportacaoService
{
    public async Task<AssistenteImportacaoResult> ImportAsync(AssistenteImportacaoForm form, CancellationToken ct)
    {
        var specialties = form.Especialidades.Where(x => x > 0).Distinct().ToArray();
        var procedures = form.Procedimentos.Where(x => x > 0).Distinct().ToArray();
        if (specialties.Length == 0) throw new AssistenteImportacaoException("Selecione ao menos uma especialidade.");

        string scriptTitle;
        string mrdTitle;
        string scriptContent;
        short tipoScript;

        if (form.CodScriptLaudoOrigem.HasValue && form.CodScriptLaudoOrigem.Value > 0)
        {
            var codOrigem = form.CodScriptLaudoOrigem.Value;
            if (publicacao.Enabled)
                return await publicacao.ImportSource(codOrigem, specialties, procedures, ct);
            var loaded = await LoadFromReferenciasAsync(form, codOrigem, ct);
            return await PersistImportAsync(
                loaded.ScriptTitle, loaded.MrdTitle, loaded.ScriptContent, loaded.MrdContent,
                loaded.TipoScript, specialties, procedures, ct);
        }

        scriptTitle = AssistenteImportacaoValidator.ValidateTitle(form.TituloScript, "Título do script", 252);
        mrdTitle = AssistenteImportacaoValidator.ValidateTitle(form.TituloMrd, "Título do MRD", 128);
        if (form.ArquivoScript is null) throw new AssistenteImportacaoException("Selecione um script de origem ou envie o arquivo do script.");
        if (form.ArquivoMrd is null) throw new AssistenteImportacaoException("O arquivo MRD é obrigatório.");
        var scriptBytes = await ReadFileAsync(form.ArquivoScript, "O arquivo do script", ct);
        var mrdBytes = await ReadFileAsync(form.ArquivoMrd, "O arquivo MRD", ct);
        scriptContent = AssistenteImportacaoValidator.PrepareScript(form.TipoScript, form.ArquivoScript.FileName, scriptBytes);
        var mrdContent = AssistenteImportacaoValidator.PrepareMrd(form.ArquivoMrd.FileName, mrdBytes);
        tipoScript = form.TipoScript;
        return await PersistImportAsync(scriptTitle, mrdTitle, scriptContent, mrdContent, tipoScript, specialties, procedures, ct);
    }

    public async Task<AssistenteImportacaoLoteResult> ImportManyAsync(AssistenteImportacaoLoteRequest request, CancellationToken ct)
    {
        var scriptIds = request.CodigosScriptLaudoOrigem.Where(x => x > 0).Distinct().ToArray();
        if (scriptIds.Length == 0) throw new AssistenteImportacaoException("Selecione ao menos um script de origem.");
        if (!request.Especialidades.Any(x => x > 0)) throw new AssistenteImportacaoException("Selecione ao menos uma especialidade.");

        var results = new List<AssistenteImportacaoLoteItemResult>();
        foreach (var scriptId in scriptIds)
        {
            try
            {
                var result = await ImportAsync(new AssistenteImportacaoForm
                {
                    CodScriptLaudoOrigem = scriptId,
                    Sistema = request.Sistema,
                    TipoScript = IsLaudosUx(request.Sistema) ? (short)3 : (short)0,
                    Especialidades = request.Especialidades,
                    Procedimentos = request.Procedimentos
                }, ct);
                results.Add(new AssistenteImportacaoLoteItemResult(scriptId, result.CodScriptLaudo, result.CodPagFotos, true, null));
            }
            catch (AssistenteImportacaoException ex)
            {
                results.Add(new AssistenteImportacaoLoteItemResult(scriptId, null, null, false, ex.Message));
            }
        }

        var imported = results.Count(x => x.Importado);
        return new AssistenteImportacaoLoteResult(scriptIds.Length, imported, results.Count - imported, results);
    }

    private async Task<ReferenciasImportPayload> LoadFromReferenciasAsync(
        AssistenteImportacaoForm form, int codOrigem, CancellationToken ct)
    {
        var script = await scriptsService.GetScriptAsync(codOrigem);
        if (script is null)
            throw new AssistenteImportacaoException($"Script de origem {codOrigem} não encontrado em /scripts/pacotes.");

        var importAsUx = IsLaudosUx(script.Sistema) || IsLaudosUx(form.Sistema) || form.TipoScript == 3;

        if (!importAsUx && !string.Equals(script.Sistema.Trim(), "Laudos Flex", StringComparison.OrdinalIgnoreCase))
            throw new AssistenteImportacaoException("Somente scripts Laudos Flex ou Laudos UX podem ser importados.");

        byte[]? scriptBytes;
        short tipoScript;

        if (importAsUx)
        {
            scriptBytes = await scriptsService.ExportJsonAsync(codOrigem);
            tipoScript = 3;
        }
        else
        {
            scriptBytes = await scriptsService.ExportDllAsync(codOrigem);
            tipoScript = AssistenteScriptEncoding.ResolveTipoScript(script.Sistema, script.Linguagem, scriptBytes ?? []);
        }

        if (scriptBytes is null || scriptBytes.Length == 0)
            throw new AssistenteImportacaoException("O script selecionado não possui DLL/JSON disponível para importação.");

        var scriptTitle = AssistenteImportacaoValidator.ValidateTitle(
            string.IsNullOrWhiteSpace(form.TituloScript) ? script.Nome : form.TituloScript,
            "Título do script", 252);

        var mrdExport = await scriptsService.ExportMrdAsync(codOrigem, form.CodScriptMrdOrigem);
        if (mrdExport is null && tipoScript != 3)
            throw new AssistenteImportacaoException("O script selecionado não possui MRD padrão para importação.");

        var scriptContent = AssistenteScriptEncoding.PrepareFromReferencias(tipoScript, scriptBytes);
        string? mrdTitle = null;
        string? mrdContent = null;
        if (mrdExport is not null)
        {
            mrdTitle = AssistenteImportacaoValidator.ValidateTitle(
                string.IsNullOrWhiteSpace(form.TituloMrd)
                    ? Path.GetFileNameWithoutExtension(mrdExport.Value.filename)
                    : form.TituloMrd,
                "Título do MRD", 128);
            mrdContent = AssistenteImportacaoValidator.PrepareMrd(mrdExport.Value.filename, mrdExport.Value.content);
        }
        return new ReferenciasImportPayload(scriptTitle, mrdTitle, scriptContent, mrdContent, tipoScript);
    }

    private sealed record ReferenciasImportPayload(
        string ScriptTitle, string? MrdTitle, string ScriptContent, string? MrdContent, short TipoScript);

    private async Task<AssistenteImportacaoResult> PersistImportAsync(
        string scriptTitle, string? mrdTitle, string scriptContent, string? mrdContent,
        short tipoScript, int[] specialties, int[] procedures, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var duplicateScript = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                "SELECT FIRST 1 CODSCRIPTLAUDO FROM SCRIPTLAUDO WHERE UPPER(TRIM(TITULO)) = UPPER(@Title)",
                new { Title = scriptTitle }, transaction, cancellationToken: ct));
            if (duplicateScript.HasValue)
                throw new AssistenteImportacaoException($"Já existe um script com este título (código {duplicateScript.Value}).");

            var hasMrd = !string.IsNullOrWhiteSpace(mrdTitle) && !string.IsNullOrWhiteSpace(mrdContent);
            if (hasMrd)
            {
                var duplicateMrd = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                    "SELECT FIRST 1 CODPAGFOTOS FROM PAGFOTOS WHERE UPPER(TRIM(TITULO)) = UPPER(@Title)",
                    new { Title = mrdTitle }, transaction, cancellationToken: ct));
                if (duplicateMrd.HasValue)
                    throw new AssistenteImportacaoException($"Já existe um MRD com este título (código {duplicateMrd.Value}).");
            }

            await RequireIdsAsync(connection, transaction, "ESPECIALIDADE", "CODESPECIALIDADE", specialties, "especialidade", ct);
            await RequireIdsAsync(connection, transaction, "PROCEDIMENTO", "CODPROCEDIMENTO", procedures, "procedimento", ct);

            var scriptParams = BlobParameters();
            scriptParams.Add("Title", scriptTitle);
            AddBlob(scriptParams, "Content", scriptContent);
            scriptParams.Add("Type", tipoScript);
            var scriptId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO SCRIPTLAUDO (TITULO, ESTRUTURASCRIPT, TIPOSCRIPT, STATUS, DATAMODIFICACAO)
                VALUES (@Title, @Content, @Type, -1, CURRENT_TIMESTAMP)
                RETURNING CODSCRIPTLAUDO
                """, scriptParams, transaction, cancellationToken: ct));

            int? mrdId = null;
            if (hasMrd)
            {
                var mrdParams = BlobParameters();
                mrdParams.Add("Title", mrdTitle);
                AddBlob(mrdParams, "Content", mrdContent!);
                mrdId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO PAGFOTOS (TITULO, ESTRUTURAPAGFOTOS, STATUS)
                    VALUES (@Title, @Content, -1)
                    RETURNING CODPAGFOTOS
                    """, mrdParams, transaction, cancellationToken: ct));

                var sequence = await connection.ExecuteScalarAsync<short>(new CommandDefinition("""
                    SELECT CAST(COALESCE(MAX(SEQUENCIA), 0) + 1 AS SMALLINT)
                    FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO = @ScriptId
                    """, new { ScriptId = scriptId }, transaction, cancellationToken: ct));
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO SCRIPTLAUDO_PAGFOTOS (CODSCRIPTLAUDO, CODPAGFOTOS, SEQUENCIA)
                    VALUES (@ScriptId, @MrdId, @Sequence)
                    """, new { ScriptId = scriptId, MrdId = mrdId.Value, Sequence = sequence }, transaction, cancellationToken: ct));
            }

            foreach (var specialtyId in specialties)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO SCRIPTLAUDO_ESPECIALIDADE (CODSCRIPTLAUDO, CODESPECIALIDADE) VALUES (@ScriptId, @SpecialtyId)",
                    new { ScriptId = scriptId, SpecialtyId = specialtyId }, transaction, cancellationToken: ct));
                if (mrdId.HasValue)
                    await connection.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO PAGFOTOS_ESPECIALIDADE (CODPAGFOTOS, CODESPECIALIDADE) VALUES (@MrdId, @SpecialtyId)",
                        new { MrdId = mrdId.Value, SpecialtyId = specialtyId }, transaction, cancellationToken: ct));
            }

            foreach (var procedureId in procedures)
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO PROCEDIMENTO_SCRIPTLAUDO (CODPROCEDIMENTO, CODSCRIPTLAUDO) VALUES (@ProcedureId, @ScriptId)",
                    new { ProcedureId = procedureId, ScriptId = scriptId }, transaction, cancellationToken: ct));

            await transaction.CommitAsync(ct);
            return new AssistenteImportacaoResult(scriptId, mrdId, specialties, procedures.Length);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AssistenteArquivoPreparado?> DownloadScriptAsync(int id, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<StoredScript>(new CommandDefinition(
            "SELECT TITULO AS Title, TIPOSCRIPT AS Type, ESTRUTURASCRIPT AS Content FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@Id",
            new { Id = id }, cancellationToken: ct));
        if (row is null) return null;
        var extension = row.Type switch { 2 => ".dll", 3 => ".json", _ => ".script" };
        return new AssistenteArquivoPreparado(row.Content, SafeFileName(row.Title) + extension,
            row.Type == 3 ? "application/json" : "application/octet-stream", row.Type);
    }

    public async Task<AssistenteArquivoPreparado?> DownloadMrdAsync(int id, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<StoredMrd>(new CommandDefinition(
            "SELECT TITULO AS Title, ESTRUTURAPAGFOTOS AS Content FROM PAGFOTOS WHERE CODPAGFOTOS=@Id",
            new { Id = id }, cancellationToken: ct));
        return row is null ? null : new AssistenteArquivoPreparado(row.Content, SafeFileName(row.Title) + ".mrd", "application/octet-stream");
    }

    public async Task<AssistenteArquivoBinario?> ExportScriptPackageAsync(int id, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        var script = await LoadExportScriptAsync(connection, id, ct);
        if (script is null) return null;
        var content = BuildScriptPackage(script, await LoadLinkedMrdsAsync(connection, id, ct));
        return new AssistenteArquivoBinario(SafeFileName(script.Title) + ".zip", content, "application/zip");
    }

    public async Task<AssistenteArquivoBinario?> ExportScriptsPackageAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var cleanIds = ids.Where(id => id > 0).Distinct().ToArray();
        if (cleanIds.Length == 0) return null;

        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in cleanIds)
            {
                var script = await LoadExportScriptAsync(connection, id, ct);
                if (script is null) continue;
                var folder = UniqueZipName(used, SafeFileName(script.Title), "");
                AddFile(archive, used, $"{folder}/{ScriptFileName(script)}", DecodeScript(script));
                foreach (var mrd in await LoadLinkedMrdsAsync(connection, id, ct))
                    AddFile(archive, used, $"{folder}/{MrdFileName(mrd.Title)}", Convert.FromBase64String(mrd.Content));
            }
        }
        return output.Length == 0
            ? null
            : new AssistenteArquivoBinario($"scripts_assistente_{DateTime.Now:yyyyMMdd_HHmmss}.zip", output.ToArray(), "application/zip");
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, string field, CancellationToken ct)
    {
        if (file.Length <= 0) throw new AssistenteImportacaoException($"{field} não pode estar vazio.");
        if (file.Length > AssistenteImportacaoValidator.MaxFileSize) throw new AssistenteImportacaoException($"{field} excede o limite de 20 MB.");
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static async Task RequireIdsAsync(
        System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        string table, string key, int[] ids, string label, CancellationToken ct)
    {
        if (ids.Length == 0) return;
        var existing = (await connection.QueryAsync<int>(new CommandDefinition(
            $"SELECT {key} FROM {table} WHERE {key} IN @Ids", new { Ids = ids }, transaction, cancellationToken: ct))).ToHashSet();
        var missing = ids.Where(x => !existing.Contains(x)).ToArray();
        if (missing.Length > 0)
            throw new AssistenteImportacaoException($"Código(s) de {label} inexistente(s): {string.Join(", ", missing)}.");
    }

    private static DynamicParameters BlobParameters() => new();

    private static bool IsLaudosUx(string? sistema) =>
        string.Equals(sistema?.Trim(), "Laudos UX", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Firebird rejeita Size = -1 (Dapper) e Size &gt; 32767 em VARCHAR.
    /// ESTRUTURASCRIPT/ESTRUTURAPAGFOTOS são BLOB: não definir Size.
    /// </summary>
    private static void AddBlob(DynamicParameters parameters, string name, string value)
    {
        parameters.Add(name, new FirebirdTextBlob(value ?? string.Empty));
    }

    private sealed class FirebirdTextBlob(string value) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name)
        {
            var parameter = new FbParameter(name, FbDbType.Text) { Value = value };
            command.Parameters.Add(parameter);
        }
    }

    private static string SafeFileName(string title) => string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private sealed record StoredScript(string Title, short Type, string Content);
    private sealed record StoredMrd(string Title, string Content);
    private sealed record ExportScript(int Id, string Title, short Type, string Content);

    private static async Task<ExportScript?> LoadExportScriptAsync(System.Data.Common.DbConnection connection, int id, CancellationToken ct)
    {
        return await connection.QueryFirstOrDefaultAsync<ExportScript>(new CommandDefinition(
            "SELECT CODSCRIPTLAUDO AS Id, TITULO AS Title, TIPOSCRIPT AS Type, ESTRUTURASCRIPT AS Content FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@Id",
            new { Id = id }, cancellationToken: ct));
    }

    private static async Task<IReadOnlyList<StoredMrd>> LoadLinkedMrdsAsync(System.Data.Common.DbConnection connection, int scriptId, CancellationToken ct)
    {
        var rows = await connection.QueryAsync<StoredMrd>(new CommandDefinition("""
            SELECT p.TITULO AS Title, p.ESTRUTURAPAGFOTOS AS Content
            FROM SCRIPTLAUDO_PAGFOTOS sp
            JOIN PAGFOTOS p ON p.CODPAGFOTOS = sp.CODPAGFOTOS
            WHERE sp.CODSCRIPTLAUDO = @ScriptId
            ORDER BY sp.SEQUENCIA, p.TITULO
            """, new { ScriptId = scriptId }, cancellationToken: ct));
        return rows.ToList();
    }

    private static byte[] BuildScriptPackage(ExportScript script, IReadOnlyList<StoredMrd> mrds)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddFile(archive, used, ScriptFileName(script), DecodeScript(script));
            foreach (var mrd in mrds)
                AddFile(archive, used, MrdFileName(mrd.Title), Convert.FromBase64String(mrd.Content));
        }
        return output.ToArray();
    }

    private static byte[] DecodeScript(ExportScript script) =>
        script.Type is 1 or 2 ? Convert.FromBase64String(script.Content) : Encoding.UTF8.GetBytes(script.Content);

    private static string ScriptFileName(ExportScript script)
    {
        var extension = script.Type switch { 1 or 2 => ".dll", 3 => ".json", _ => ".script" };
        return WithExtension(SafeFileName(script.Title), extension);
    }

    private static string MrdFileName(string title) => WithExtension(SafeFileName(title), ".mrd");

    private static string WithExtension(string name, string extension)
    {
        var clean = string.IsNullOrWhiteSpace(name) ? "arquivo" : name;
        return Path.HasExtension(clean) ? Path.ChangeExtension(clean, extension) : clean + extension;
    }

    private static void AddFile(ZipArchive archive, HashSet<string> used, string name, byte[] content)
    {
        var entryName = UniqueZipName(used, name, Path.GetExtension(name));
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string UniqueZipName(HashSet<string> used, string name, string extension)
    {
        var candidate = name.Replace('\\', '/').Trim('/');
        if (used.Add(candidate)) return candidate;
        var directory = Path.GetDirectoryName(candidate)?.Replace('\\', '/');
        var file = Path.GetFileNameWithoutExtension(candidate);
        var ext = string.IsNullOrEmpty(extension) ? Path.GetExtension(candidate) : extension;
        for (var i = 2; ; i++)
        {
            var numbered = string.IsNullOrWhiteSpace(directory) ? $"{file}_{i}{ext}" : $"{directory}/{file}_{i}{ext}";
            if (used.Add(numbered)) return numbered;
        }
    }
}
