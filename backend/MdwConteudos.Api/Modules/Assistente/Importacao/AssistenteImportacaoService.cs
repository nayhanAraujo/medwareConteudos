using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public interface IAssistenteImportacaoService
{
    Task<AssistenteImportacaoResult> ImportAsync(AssistenteImportacaoForm form, CancellationToken ct);
    Task<AssistenteArquivoPreparado?> DownloadScriptAsync(int id, CancellationToken ct);
    Task<AssistenteArquivoPreparado?> DownloadMrdAsync(int id, CancellationToken ct);
}

public sealed class AssistenteImportacaoService(IAssistantFirebirdConnectionFactory connectionFactory) : IAssistenteImportacaoService
{
    public async Task<AssistenteImportacaoResult> ImportAsync(AssistenteImportacaoForm form, CancellationToken ct)
    {
        var scriptTitle = AssistenteImportacaoValidator.ValidateTitle(form.TituloScript, "Título do script", 252);
        var mrdTitle = AssistenteImportacaoValidator.ValidateTitle(form.TituloMrd, "Título do MRD", 128);
        var specialties = form.Especialidades.Where(x => x > 0).Distinct().ToArray();
        var procedures = form.Procedimentos.Where(x => x > 0).Distinct().ToArray();
        if (specialties.Length == 0) throw new AssistenteImportacaoException("Selecione ao menos uma especialidade.");
        if (form.ArquivoScript is null) throw new AssistenteImportacaoException("O arquivo do script é obrigatório.");
        if (form.ArquivoMrd is null) throw new AssistenteImportacaoException("O arquivo MRD é obrigatório.");

        var scriptBytes = await ReadFileAsync(form.ArquivoScript, "O arquivo do script", ct);
        var mrdBytes = await ReadFileAsync(form.ArquivoMrd, "O arquivo MRD", ct);
        var scriptContent = AssistenteImportacaoValidator.PrepareScript(form.TipoScript, form.ArquivoScript.FileName, scriptBytes);
        var mrdContent = AssistenteImportacaoValidator.PrepareMrd(form.ArquivoMrd.FileName, mrdBytes);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var duplicateScript = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                "SELECT FIRST 1 CODSCRIPTLAUDO FROM SCRIPTLAUDO WHERE UPPER(TRIM(TITULO)) = UPPER(@Title)",
                new { Title = scriptTitle }, transaction, cancellationToken: ct));
            if (duplicateScript.HasValue)
                throw new AssistenteImportacaoException($"Já existe um script com este título (código {duplicateScript.Value}).");

            var duplicateMrd = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                "SELECT FIRST 1 CODPAGFOTOS FROM PAGFOTOS WHERE UPPER(TRIM(TITULO)) = UPPER(@Title)",
                new { Title = mrdTitle }, transaction, cancellationToken: ct));
            if (duplicateMrd.HasValue)
                throw new AssistenteImportacaoException($"Já existe um MRD com este título (código {duplicateMrd.Value}).");

            await RequireIdsAsync(connection, transaction, "ESPECIALIDADE", "CODESPECIALIDADE", specialties, "especialidade", ct);
            await RequireIdsAsync(connection, transaction, "PROCEDIMENTO", "CODPROCEDIMENTO", procedures, "procedimento", ct);

            var scriptId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO SCRIPTLAUDO (TITULO, ESTRUTURASCRIPT, TIPOSCRIPT, STATUS)
                VALUES (@Title, @Content, @Type, -1)
                RETURNING CODSCRIPTLAUDO
                """, new { Title = scriptTitle, Content = scriptContent, Type = form.TipoScript }, transaction, cancellationToken: ct));

            var mrdId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO PAGFOTOS (TITULO, ESTRUTURAPAGFOTOS, STATUS)
                VALUES (@Title, @Content, -1)
                RETURNING CODPAGFOTOS
                """, new { Title = mrdTitle, Content = mrdContent }, transaction, cancellationToken: ct));

            var sequence = await connection.ExecuteScalarAsync<short>(new CommandDefinition("""
                SELECT CAST(COALESCE(MAX(SEQUENCIA), 0) + 1 AS SMALLINT)
                FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO = @ScriptId
                """, new { ScriptId = scriptId }, transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO SCRIPTLAUDO_PAGFOTOS (CODSCRIPTLAUDO, CODPAGFOTOS, SEQUENCIA)
                VALUES (@ScriptId, @MrdId, @Sequence)
                """, new { ScriptId = scriptId, MrdId = mrdId, Sequence = sequence }, transaction, cancellationToken: ct));

            foreach (var specialtyId in specialties)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO SCRIPTLAUDO_ESPECIALIDADE (CODSCRIPTLAUDO, CODESPECIALIDADE) VALUES (@ScriptId, @SpecialtyId)",
                    new { ScriptId = scriptId, SpecialtyId = specialtyId }, transaction, cancellationToken: ct));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO PAGFOTOS_ESPECIALIDADE (CODPAGFOTOS, CODESPECIALIDADE) VALUES (@MrdId, @SpecialtyId)",
                    new { MrdId = mrdId, SpecialtyId = specialtyId }, transaction, cancellationToken: ct));
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

    private static string SafeFileName(string title) => string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private sealed record StoredScript(string Title, short Type, string Content);
    private sealed record StoredMrd(string Title, string Content);
}
