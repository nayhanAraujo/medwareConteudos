using MdwConteudos.Api.Modules.Auth;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Users;

public interface IUsersService
{
    Task<IEnumerable<UserListItem>> ListAsync(CancellationToken ct = default);
    Task<UserListItem?> GetAsync(int codUsuario, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> CreateAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdateAsync(int codUsuario, UpdateUserRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int codUsuario, CancellationToken ct = default);
    Task<bool> SetStatusAsync(int codUsuario, int status, CancellationToken ct = default);
}

public class UsersService : IUsersService
{
    private readonly IFirebirdConnectionFactory _db;
    public UsersService(IFirebirdConnectionFactory db) => _db = db;

    private const string SelectUser = @"SELECT CODUSUARIO AS CodUsuario, NOME AS Nome, IDENTIFICACAO AS Identificacao,
        PERFIL AS Perfil, CAST(STATUS AS INTEGER) AS Status, EMAIL AS Email FROM USUARIO";

    public async Task<IEnumerable<UserListItem>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryAsync<UserListItem>(new CommandDefinition(SelectUser + " ORDER BY NOME", cancellationToken: ct));
    }

    public async Task<UserListItem?> GetAsync(int codUsuario, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<UserListItem>(new CommandDefinition(SelectUser + " WHERE CODUSUARIO=@codUsuario", new { codUsuario }, cancellationToken: ct));
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        var error = AccountSecurity.Validate(req.Nome, req.Identificacao, req.Perfil, req.Senha, req.ConfirmarSenha, true);
        if (error is not null) return (false, error);
        var email = RecoveryEmail.Normalize(req.Email);
        if (email is not null && !RecoveryEmail.IsValid(email)) return (false, "Informe um e-mail válido.");
        var id = req.Identificacao.Trim().ToLowerInvariant();
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM USUARIO WHERE IDENTIFICACAO=@id", new { id }) > 0)
            return (false, "Esse login já está em uso.");
        if (email is not null && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM USUARIO WHERE LOWER(TRIM(EMAIL))=@email", new { email }) > 0)
            return (false, "Esse e-mail já está em uso.");
        var hash = PasswordHasher.Sha256Hex(req.Senha);
        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO USUARIO (NOME,IDENTIFICACAO,UCASE_NOME,SENHA,PERFIL,STATUS,DTHRULTMODIFICACAO,EMAIL)
              VALUES (@nome,@id,@nome,@hash,@perfil,@status,@now,@email)",
            new { nome = req.Nome.Trim(), id, hash, perfil = req.Perfil.Trim().ToLowerInvariant(), req.Status, now = DateTime.Now, email }, cancellationToken: ct));
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(int codUsuario, UpdateUserRequest req, CancellationToken ct = default)
    {
        var error = AccountSecurity.Validate(req.Nome, req.Identificacao, req.Perfil, req.Senha, req.ConfirmarSenha, false);
        if (error is not null) return (false, error);
        var email = RecoveryEmail.Normalize(req.Email);
        if (email is not null && !RecoveryEmail.IsValid(email)) return (false, "Informe um e-mail válido.");
        var id = req.Identificacao.Trim().ToLowerInvariant();
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        if (await conn.ExecuteAsync(new CommandDefinition("UPDATE USUARIO SET DTHRULTMODIFICACAO=DTHRULTMODIFICACAO WHERE CODUSUARIO=@codUsuario", new { codUsuario }, tx, cancellationToken: ct)) != 1)
            return (false, "Usuário não encontrado.");
        var previous = await conn.QuerySingleAsync<UserListItem>(new CommandDefinition(SelectUser + " WHERE CODUSUARIO=@codUsuario", new { codUsuario }, tx, cancellationToken: ct));
        // Null (including omitted fields in older clients) preserves the email; empty string clears it.
        email = req.Email is null ? previous.Email : email;
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM USUARIO WHERE IDENTIFICACAO=@id AND CODUSUARIO<>@codUsuario", new { id, codUsuario }, tx) > 0)
            return (false, "Login já em uso por outro usuário.");
        if (email is not null && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM USUARIO WHERE LOWER(TRIM(EMAIL))=@email AND CODUSUARIO<>@codUsuario", new { email, codUsuario }, tx) > 0)
            return (false, "Esse e-mail já está em uso.");
        var hash = string.IsNullOrEmpty(req.Senha) ? null : PasswordHasher.Sha256Hex(req.Senha);
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE USUARIO SET NOME=@nome,IDENTIFICACAO=@id,SENHA=COALESCE(@hash,SENHA),PERFIL=@perfil,
                STATUS=@status,DTHRULTMODIFICACAO=@now,EMAIL=@email WHERE CODUSUARIO=@codUsuario",
            new { nome = req.Nome.Trim(), id, hash, perfil = req.Perfil.Trim().ToLowerInvariant(), req.Status, now = DateTime.Now, email, codUsuario }, tx, cancellationToken: ct));
        if (hash is not null || email != previous.Email || req.Status != -1)
            await conn.ExecuteAsync(new CommandDefinition("UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE CODUSUARIO=@codUsuario AND USED_UTC IS NULL", new { codUsuario, now = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return (true, null);
    }

    public async Task<bool> SetStatusAsync(int codUsuario, int status, CancellationToken ct = default)
    {
        if (status is not (-1 or 0)) throw new ArgumentOutOfRangeException(nameof(status));
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE USUARIO SET STATUS=@status,DTHRULTMODIFICACAO=@now WHERE CODUSUARIO=@codUsuario",
            new { status, now = DateTime.Now, codUsuario }, tx, cancellationToken: ct));
        if (rows == 0) return false;
        if (status == 0)
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE PASSWORD_RESET SET USED_UTC=@now WHERE CODUSUARIO=@codUsuario AND USED_UTC IS NULL",
                new { codUsuario, now = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int codUsuario, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM USUARIO WHERE CODUSUARIO=@codUsuario", new { codUsuario }, cancellationToken: ct)) > 0;
    }
}
