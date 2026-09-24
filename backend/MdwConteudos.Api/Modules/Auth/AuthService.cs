using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string identificacao, string senha, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ForgotPasswordAsync(string identificacao, string newPassword, CancellationToken ct = default);
    string CreateWebToken(SessionUser user);
    SessionUser? ParseWebToken(string token);
}

file class UsuarioRow
{
    public int CODUSUARIO { get; set; }
    public string NOME { get; set; } = "";
    public string PERFIL { get; set; } = "";
}

public class AuthService : IAuthService
{
    private readonly IFirebirdConnectionFactory _db;
    private readonly WebAuthOptions _auth;
    private readonly bool _development;

    public AuthService(IFirebirdConnectionFactory db, IOptions<WebAuthOptions> auth, IHostEnvironment? environment = null)
    {
        _db = db;
        _auth = auth.Value;
        _development = environment?.IsDevelopment() == true;
    }

    public async Task<LoginResponse> LoginAsync(string identificacao, string senha, CancellationToken ct = default)
    {
        var id = identificacao.Trim().ToLowerInvariant();
        if (IsDevUser(id, senha))
        {
            await using var devConn = await _db.OpenConnectionAsync(ct);
            var codUsuario = await devConn.QueryFirstOrDefaultAsync<int?>(
                "SELECT FIRST 1 CODUSUARIO FROM USUARIO WHERE LOWER(IDENTIFICACAO) = @id AND STATUS = -1",
                new { id });
            if (!codUsuario.HasValue)
                return new LoginResponse(false, null, null, "O usuário de desenvolvimento não possui um cadastro ativo no banco.");
            var devUser = new SessionUser(codUsuario.Value, _auth.DevName, _auth.DevRole);
            return new LoginResponse(true, CreateWebToken(devUser), devUser, null);
        }

        var hash = PasswordHasher.Sha256Hex(senha);
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<UsuarioRow>(
            @"SELECT CODUSUARIO, NOME, PERFIL FROM USUARIO
              WHERE IDENTIFICACAO = @id AND SENHA = @hash AND STATUS = -1",
            new { id, hash });
        if (row is null)
            return new LoginResponse(false, null, null, "Usuário ou senha inválidos.");

        var user = new SessionUser(row.CODUSUARIO, row.NOME, row.PERFIL);
        return new LoginResponse(true, CreateWebToken(user), user, null);
    }

    private bool IsDevUser(string normalizedIdentificacao, string senha)
    {
        if (!_development || !_auth.DevUserEnabled) return false;
        if (string.IsNullOrWhiteSpace(_auth.DevUsername) || string.IsNullOrWhiteSpace(_auth.DevPassword)) return false;
        return normalizedIdentificacao == _auth.DevUsername.Trim().ToLowerInvariant()
            && senha == _auth.DevPassword;
    }

    public Task<(bool Ok, string? Error)> ForgotPasswordAsync(string identificacao, string newPassword, CancellationToken ct = default)
    {
        return Task.FromResult<(bool Ok, string? Error)>((false, "Redefinição anônima de senha desativada."));
    }

    public string CreateWebToken(SessionUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_auth.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.CodUsuario.ToString()),
            new Claim(ClaimTypes.Name, user.Nome),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var token = new JwtSecurityToken(
            _auth.Issuer,
            _auth.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_auth.ExpirationMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public SessionUser? ParseWebToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _auth.Issuer,
                ValidateAudience = true,
                ValidAudience = _auth.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_auth.JwtSecret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            var id = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var nome = principal.FindFirst(ClaimTypes.Name)!.Value;
            var role = principal.FindFirst(ClaimTypes.Role)!.Value;
            return new SessionUser(id, nome, role);
        }
        catch
        {
            return null;
        }
    }
}
