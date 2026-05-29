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
}

public class UsersService : IUsersService
{
    private readonly IFirebirdConnectionFactory _db;

    public UsersService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<IEnumerable<UserListItem>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryAsync<UserListItem>(@"
            SELECT CODUSUARIO AS CodUsuario, NOME AS Nome, IDENTIFICACAO AS Identificacao,
                   PERFIL AS Perfil, STATUS AS Status FROM USUARIO ORDER BY NOME");
    }

    public async Task<UserListItem?> GetAsync(int codUsuario, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<UserListItem>(@"
            SELECT CODUSUARIO AS CodUsuario, NOME AS Nome, IDENTIFICACAO AS Identificacao,
                   PERFIL AS Perfil, STATUS AS Status FROM USUARIO WHERE CODUSUARIO = @codUsuario",
            new { codUsuario });
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        if (req.Senha != req.ConfirmarSenha) return (false, "As senhas não coincidem.");
        if (req.Senha.Length < 6) return (false, "Senha deve ter no mínimo 6 caracteres.");
        var id = req.Identificacao.Trim().ToLowerInvariant();
        if (id.Contains(' ')) return (false, "Login não pode conter espaços.");

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>("SELECT 1 FROM USUARIO WHERE IDENTIFICACAO = @id", new { id });
        if (exists == 1) return (false, "Esse login já está em uso.");

        var hash = PasswordHasher.Sha256Hex(req.Senha);
        await conn.ExecuteAsync(
            @"INSERT INTO USUARIO (NOME, IDENTIFICACAO, UCASE_NOME, SENHA, PERFIL, STATUS, DTHRULTMODIFICACAO)
              VALUES (@nome, @id, @nome, @hash, @perfil, @status, @now)",
            new { nome = req.Nome.Trim(), id, hash, req.Perfil, req.Status, now = DateTime.Now });
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(int codUsuario, UpdateUserRequest req, CancellationToken ct = default)
    {
        var id = req.Identificacao.Trim().ToLowerInvariant();
        await using var conn = await _db.OpenConnectionAsync(ct);
        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT 1 FROM USUARIO WHERE IDENTIFICACAO = @id AND CODUSUARIO <> @codUsuario",
            new { id, codUsuario });
        if (dup == 1) return (false, "Login já em uso por outro usuário.");

        if (!string.IsNullOrEmpty(req.Senha))
        {
            if (req.Senha != req.ConfirmarSenha) return (false, "As senhas não coincidem.");
            var hash = PasswordHasher.Sha256Hex(req.Senha);
            await conn.ExecuteAsync(
                @"UPDATE USUARIO SET NOME=@nome, IDENTIFICACAO=@id, SENHA=@hash, PERFIL=@perfil, STATUS=@status, DTHRULTMODIFICACAO=@now
                  WHERE CODUSUARIO=@codUsuario",
                new { nome = req.Nome, id, hash, req.Perfil, req.Status, now = DateTime.Now, codUsuario });
        }
        else
        {
            await conn.ExecuteAsync(
                @"UPDATE USUARIO SET NOME=@nome, IDENTIFICACAO=@id, PERFIL=@perfil, STATUS=@status, DTHRULTMODIFICACAO=@now
                  WHERE CODUSUARIO=@codUsuario",
                new { nome = req.Nome, id, req.Perfil, req.Status, now = DateTime.Now, codUsuario });
        }
        return (true, null);
    }

    public async Task<bool> DeleteAsync(int codUsuario, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync("DELETE FROM USUARIO WHERE CODUSUARIO = @codUsuario", new { codUsuario });
        return rows > 0;
    }
}
