using FirebirdSql.Data.FirebirdClient;
using System.Data;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auth;

public sealed class FirebirdPasswordRecoveryStore(IFirebirdConnectionFactory db) : IPasswordRecoveryStore
{
    public async Task<string?> IssueAsync(string email, string hash, DateTime now, CancellationToken ct)
    {
        try { return await IssueCoreAsync(email, hash, now, ct); }
        catch (FbException ex) when (ex.ErrorCode is 335544336 or 335544451 or 335544345) { return null; }
    }

    private async Task<string?> IssueCoreAsync(string email, string hash, DateTime now, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Serialize issuance, redemption and account edits on the same user row.
        var id = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT CODUSUARIO FROM USUARIO WHERE LOWER(TRIM(EMAIL))=@email AND STATUS=-1", new { email }, tx, cancellationToken: ct));
        if (id is null) return null;
        await conn.ExecuteAsync(new CommandDefinition("UPDATE USUARIO SET DTHRULTMODIFICACAO=DTHRULTMODIFICACAO WHERE CODUSUARIO=@id", new { id }, tx, cancellationToken: ct));
        var recipient = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT EMAIL FROM USUARIO WHERE CODUSUARIO=@id AND LOWER(TRIM(EMAIL))=@email AND STATUS=-1", new { id, email }, tx, cancellationToken: ct));
        if (recipient is null) return null;
        var recent = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM PASSWORD_RESET WHERE CODUSUARIO=@id AND CREATED_UTC>@cutoff", new { id, cutoff = now.AddMinutes(-1) }, tx, cancellationToken: ct));
        if (recent > 0) return null;
        await conn.ExecuteAsync(new CommandDefinition("UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE CODUSUARIO=@id AND USED_UTC IS NULL", new { id, now }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO PASSWORD_RESET (TOKEN_HASH,CODUSUARIO,CREATED_UTC,EXPIRES_UTC) VALUES (@hash,@id,@now,@expires)",
            new { hash, id, now, expires = now.AddMinutes(30) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return recipient;
    }

    public async Task InvalidateAsync(string hash, DateTime now, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE TOKEN_HASH=@hash AND USED_UTC IS NULL", new { hash, now }, cancellationToken: ct));
    }

    public async Task<bool> RedeemAsync(string hash, string passwordHash, DateTime now, CancellationToken ct)
    {
        try { return await RedeemCoreAsync(hash, passwordHash, now, ct); }
        // Firebird reports competing row updates as conflicts; the losing transaction rolls back.
        catch (FbException ex) when (ex.ErrorCode is 335544336 or 335544451 or 335544345) { return false; }
    }

    private async Task<bool> RedeemCoreAsync(string hash, string passwordHash, DateTime now, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var id = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT CODUSUARIO FROM PASSWORD_RESET WHERE TOKEN_HASH=@hash", new { hash }, tx, cancellationToken: ct));
        if (id is null) return false;
        var active = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE USUARIO SET DTHRULTMODIFICACAO=DTHRULTMODIFICACAO WHERE CODUSUARIO=@id AND STATUS=-1", new { id }, tx, cancellationToken: ct));
        if (active != 1) return false;
        var consumed = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE TOKEN_HASH=@hash AND USED_UTC IS NULL AND EXPIRES_UTC>@now", new { hash, now }, tx, cancellationToken: ct));
        if (consumed != 1) return false;
        await conn.ExecuteAsync(new CommandDefinition("UPDATE USUARIO SET SENHA=@passwordHash,DTHRULTMODIFICACAO=@now WHERE CODUSUARIO=@id", new { passwordHash, now, id }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition("UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE CODUSUARIO=@id AND USED_UTC IS NULL", new { id, now }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return true;
    }
}
