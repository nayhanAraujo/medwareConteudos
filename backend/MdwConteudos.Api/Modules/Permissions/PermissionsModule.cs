using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Permissions;

public static class PermissionDomains
{
    public const string Biblioteca = "biblioteca";
    public const string Variaveis = "variaveis";
    public const string Formulas = "formulas";
    public const string Referencias = "referencias";
    public const string Scripts = "scripts";
    public const string Pacotes = "pacotes";
    public const string Conteudos = "conteudos";
    public const string Relatorios = "relatorios";
    public const string Paineis = "paineis";
    public const string Assistente = "assistente";
    public const string Usuarios = "usuarios";
    public const string Configuracoes = "configuracoes";
    public const string AprovacaoConteudo = "aprovacao-conteudo";
    public const string Studio = "studio";
}

public static class PermissionActions
{
    public const string Visualizar = "visualizar";
    public const string Criar = "criar";
    public const string Editar = "editar";
    public const string Excluir = "excluir";
    public const string Importar = "importar";
    public const string Exportar = "exportar";
    public const string Aprovar = "aprovar";
    public const string Vincular = "vincular";
    public const string Ativar = "ativar";
    public const string Converter = "converter";
    public const string Voz = "voz";
}

public sealed record PermissionCatalogItem(string Chave, string Dominio, string Acao, string Descricao, int Status);
public sealed record ProfilePermissionState(string Perfil, IReadOnlySet<string> Permissoes);
public sealed record UserPermissionOverride(string Chave, string Modo);
public sealed record UserPermissionState(int CodUsuario, string Perfil, IReadOnlyList<UserPermissionOverride> Excecoes, IReadOnlySet<string> Efetivas);
public sealed record UpdateProfilePermissionsRequest(IReadOnlyList<string>? Permissoes);
public sealed record UpdateUserPermissionsRequest(IReadOnlyList<UserPermissionOverride>? Excecoes);

public static class PermissionsModuleExtensions
{
    public static IServiceCollection AddPermissionsModule(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }
}

public interface IPermissionService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task ValidateSchemaAsync(CancellationToken ct = default) => throw new NotSupportedException("Schema validation not implemented.");
    Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default);
    Task<ProfilePermissionState> GetProfileAsync(string perfil, CancellationToken ct = default);
    Task SetProfileAsync(string perfil, IReadOnlyList<string> permissoes, CancellationToken ct = default);
    Task<UserPermissionState> GetUserAsync(int codUsuario, CancellationToken ct = default);
    Task SetUserAsync(int codUsuario, IReadOnlyList<UserPermissionOverride> excecoes, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int codUsuario, string perfil, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(int codUsuario, string perfil, string dominio, string acao, CancellationToken ct = default);
}

public sealed class PermissionService(IFirebirdConnectionFactory db) : IPermissionService
{
    private static readonly PermissionCatalogItem[] Seed = BuildSeed();

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var firstInstall = !await TableExistsAsync(conn, "PERMISSAO");
        if (firstInstall)
        {
            await conn.ExecuteAsync("""
                CREATE TABLE PERMISSAO (
                    CHAVE VARCHAR(80) NOT NULL,
                    DOMINIO VARCHAR(40) NOT NULL,
                    ACAO VARCHAR(40) NOT NULL,
                    DESCRICAO VARCHAR(160) NOT NULL,
                    STATUS SMALLINT NOT NULL,
                    DTHRULTMODIFICACAO TIMESTAMP,
                    CONSTRAINT PK_PERMISSAO PRIMARY KEY (CHAVE)
                )
                """);
        }

        if (!await TableExistsAsync(conn, "PERFIL_PERMISSAO"))
        {
            await conn.ExecuteAsync("""
                CREATE TABLE PERFIL_PERMISSAO (
                    PERFIL VARCHAR(20) NOT NULL,
                    CHAVE VARCHAR(80) NOT NULL,
                    DTHRULTMODIFICACAO TIMESTAMP,
                    CONSTRAINT PK_PERFIL_PERMISSAO PRIMARY KEY (PERFIL, CHAVE)
                )
                """);
        }

        if (!await TableExistsAsync(conn, "USUARIO_PERMISSAO"))
        {
            await conn.ExecuteAsync("""
                CREATE TABLE USUARIO_PERMISSAO (
                    CODUSUARIO INTEGER NOT NULL,
                    CHAVE VARCHAR(80) NOT NULL,
                    MODO VARCHAR(10) NOT NULL,
                    DTHRULTMODIFICACAO TIMESTAMP,
                    CONSTRAINT PK_USUARIO_PERMISSAO PRIMARY KEY (CODUSUARIO, CHAVE)
                )
                """);
        }

        await conn.ExecuteAsync("UPDATE PERMISSAO SET STATUS = 0, DTHRULTMODIFICACAO = CURRENT_TIMESTAMP");

        foreach (var item in Seed)
        {
            await conn.ExecuteAsync("""
                UPDATE OR INSERT INTO PERMISSAO (CHAVE, DOMINIO, ACAO, DESCRICAO, STATUS, DTHRULTMODIFICACAO)
                VALUES (@Chave, @Dominio, @Acao, @Descricao, @Status, CURRENT_TIMESTAMP)
                MATCHING (CHAVE)
                """, item);
        }

        foreach (var perfil in firstInstall ? new[] { "usuario", "comum" } : Array.Empty<string>())
        {
            foreach (var chave in Seed.Where(x => x.Acao == PermissionActions.Visualizar && x.Dominio != PermissionDomains.Studio).Select(x => x.Chave))
            {
                await conn.ExecuteAsync("""
                    UPDATE OR INSERT INTO PERFIL_PERMISSAO (PERFIL, CHAVE, DTHRULTMODIFICACAO)
                    VALUES (@perfil, @chave, CURRENT_TIMESTAMP)
                    MATCHING (PERFIL, CHAVE)
                    """, new { perfil, chave });
            }
        }
    }

    public async Task ValidateSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        foreach (var table in new[] { "PERMISSAO", "PERFIL_PERMISSAO", "USUARIO_PERMISSAO" })
            if (!await TableExistsAsync(conn, table))
                throw new InvalidOperationException("Schema de permissões pendente. Execute --migrate-permissions antes de iniciar.");
        var keys = (await conn.QueryAsync<string>("SELECT CHAVE FROM PERMISSAO WHERE STATUS = -1")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Seed.Any(item => !keys.Contains(item.Chave)))
            throw new InvalidOperationException("Catálogo de permissões desatualizado. Execute --migrate-permissions.");
    }

    public async Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PermissionCatalogItem>("""
            SELECT CHAVE AS Chave, DOMINIO AS Dominio, ACAO AS Acao, DESCRICAO AS Descricao, CAST(STATUS AS INTEGER) AS Status
            FROM PERMISSAO
            WHERE STATUS = -1
            ORDER BY DOMINIO, ACAO
            """);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<string>("""
            SELECT DISTINCT PERFIL
            FROM USUARIO
            WHERE PERFIL IS NOT NULL AND TRIM(PERFIL) <> ''
            ORDER BY PERFIL
            """);
        return rows.Select(NormalizeProfile).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<ProfilePermissionState> GetProfileAsync(string perfil, CancellationToken ct = default)
    {
        var normalized = NormalizeProfile(perfil);
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<string>("""
            SELECT pp.CHAVE
            FROM PERFIL_PERMISSAO pp
            JOIN PERMISSAO p ON p.CHAVE = pp.CHAVE AND p.STATUS = -1
            WHERE LOWER(pp.PERFIL) = @perfil
            ORDER BY pp.CHAVE
            """, new { perfil = normalized });
        return new ProfilePermissionState(normalized, rows.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public async Task SetProfileAsync(string perfil, IReadOnlyList<string> permissoes, CancellationToken ct = default)
    {
        var normalized = NormalizeProfile(perfil);
        if (normalized == "admin") throw new InvalidOperationException("O perfil admin possui acesso total por regra fixa.");

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var valid = await ValidKeysAsync(conn, permissoes, tx);
            await conn.ExecuteAsync("DELETE FROM PERFIL_PERMISSAO WHERE LOWER(PERFIL) = @perfil", new { perfil = normalized }, tx);
            foreach (var chave in valid)
            {
                await conn.ExecuteAsync("""
                    INSERT INTO PERFIL_PERMISSAO (PERFIL, CHAVE, DTHRULTMODIFICACAO)
                    VALUES (@perfil, @chave, CURRENT_TIMESTAMP)
                    """, new { perfil = normalized, chave }, tx);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<UserPermissionState> GetUserAsync(int codUsuario, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var perfil = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT PERFIL FROM USUARIO WHERE CODUSUARIO = @codUsuario",
            new { codUsuario });
        if (perfil is null) throw new KeyNotFoundException("Usuario nao encontrado.");

        var overrides = (await conn.QueryAsync<UserPermissionOverride>("""
            SELECT up.CHAVE AS Chave, up.MODO AS Modo
            FROM USUARIO_PERMISSAO up
            JOIN PERMISSAO p ON p.CHAVE = up.CHAVE AND p.STATUS = -1
            WHERE up.CODUSUARIO = @codUsuario
            ORDER BY up.CHAVE
            """, new { codUsuario })).ToList();
        var effective = await GetEffectivePermissionsAsync(codUsuario, perfil, ct);
        return new UserPermissionState(codUsuario, NormalizeProfile(perfil), overrides, effective);
    }

    public async Task SetUserAsync(int codUsuario, IReadOnlyList<UserPermissionOverride> excecoes, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM USUARIO WHERE CODUSUARIO = @codUsuario",
                new { codUsuario }, tx);
            if (exists == 0) throw new KeyNotFoundException("Usuario nao encontrado.");

            var normalized = excecoes
                .Select(x => new UserPermissionOverride(x.Chave.Trim().ToLowerInvariant(), NormalizeMode(x.Modo)))
                .Where(x => x.Chave.Length > 0)
                .GroupBy(x => x.Chave, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            var valid = await ValidKeysAsync(conn, normalized.Select(x => x.Chave).ToList(), tx);
            await conn.ExecuteAsync("DELETE FROM USUARIO_PERMISSAO WHERE CODUSUARIO = @codUsuario", new { codUsuario }, tx);
            foreach (var item in normalized.Where(x => valid.Contains(x.Chave, StringComparer.OrdinalIgnoreCase)))
            {
                await conn.ExecuteAsync("""
                    INSERT INTO USUARIO_PERMISSAO (CODUSUARIO, CHAVE, MODO, DTHRULTMODIFICACAO)
                    VALUES (@codUsuario, @Chave, @Modo, CURRENT_TIMESTAMP)
                    """, new { codUsuario, item.Chave, item.Modo }, tx);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int codUsuario, string perfil, CancellationToken ct = default)
    {
        var normalized = NormalizeProfile(perfil);
        if (normalized == "admin")
            return Seed.Select(x => x.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var conn = await db.OpenConnectionAsync(ct);
        var profileKeys = (await conn.QueryAsync<string>("""
            SELECT pp.CHAVE
            FROM PERFIL_PERMISSAO pp
            JOIN PERMISSAO p ON p.CHAVE = pp.CHAVE AND p.STATUS = -1
            WHERE LOWER(pp.PERFIL) = @perfil
            """, new { perfil = normalized })).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overrides = await conn.QueryAsync<UserPermissionOverride>("""
            SELECT up.CHAVE AS Chave, up.MODO AS Modo
            FROM USUARIO_PERMISSAO up
            JOIN PERMISSAO p ON p.CHAVE = up.CHAVE AND p.STATUS = -1
            WHERE up.CODUSUARIO = @codUsuario
            """, new { codUsuario });

        foreach (var item in overrides)
        {
            var mode = NormalizeMode(item.Modo);
            if (mode == "NEGAR") profileKeys.Remove(item.Chave);
            else if (mode == "PERMITIR") profileKeys.Add(item.Chave);
        }

        return profileKeys;
    }

    public async Task<bool> HasPermissionAsync(int codUsuario, string perfil, string dominio, string acao, CancellationToken ct = default)
    {
        if (NormalizeProfile(perfil) == "admin") return true;
        var permissions = await GetEffectivePermissionsAsync(codUsuario, perfil, ct);
        return permissions.Contains(Key(dominio, acao));
    }

    private static async Task<bool> TableExistsAsync(System.Data.IDbConnection conn, string table)
        => await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = @table",
            new { table = table.ToUpperInvariant() }) > 0;

    private static async Task<HashSet<string>> ValidKeysAsync(
        System.Data.IDbConnection conn,
        IReadOnlyList<string> keys,
        System.Data.IDbTransaction tx)
    {
        var normalized = keys.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        if (normalized.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = await conn.QueryAsync<string>(
            "SELECT CHAVE FROM PERMISSAO WHERE STATUS = -1 AND CHAVE IN @keys",
            new { keys = normalized }, tx);
        var result = valid.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = normalized.Where(x => !result.Contains(x)).ToList();
        if (invalid.Count > 0) throw new InvalidOperationException($"Permissao invalida: {invalid[0]}.");
        return result;
    }

    private static string NormalizeProfile(string? value) => (value ?? "").Trim().ToLowerInvariant();
    private static string NormalizeMode(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        if (normalized is "PERMITIR" or "NEGAR") return normalized;
        throw new InvalidOperationException("Modo de excecao invalido. Use PERMITIR ou NEGAR.");
    }

    public static string Key(string dominio, string acao)
        => $"{dominio.Trim().ToLowerInvariant()}.{acao.Trim().ToLowerInvariant()}";

    private static PermissionCatalogItem[] BuildSeed()
    {
        var map = new Dictionary<string, string[]>
        {
            [PermissionDomains.Studio] = [PermissionActions.Visualizar, PermissionActions.Converter, PermissionActions.Voz],
            [PermissionDomains.Biblioteca] = [PermissionActions.Visualizar],
            [PermissionDomains.AprovacaoConteudo] = [PermissionActions.Visualizar, PermissionActions.Aprovar],
            [PermissionDomains.Conteudos] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Ativar],
            [PermissionDomains.Relatorios] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Aprovar, PermissionActions.Ativar],
            [PermissionDomains.Paineis] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Exportar, PermissionActions.Ativar],
            [PermissionDomains.Assistente] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Aprovar, PermissionActions.Vincular, PermissionActions.Ativar],
            [PermissionDomains.Usuarios] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Ativar],
            [PermissionDomains.Configuracoes] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir],
            [PermissionDomains.Variaveis] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Vincular, PermissionActions.Ativar],
            [PermissionDomains.Formulas] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Vincular],
            [PermissionDomains.Referencias] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Vincular],
            [PermissionDomains.Scripts] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Importar, PermissionActions.Exportar, PermissionActions.Aprovar, PermissionActions.Vincular, PermissionActions.Ativar],
            [PermissionDomains.Pacotes] = [PermissionActions.Visualizar, PermissionActions.Criar, PermissionActions.Editar, PermissionActions.Excluir, PermissionActions.Vincular, PermissionActions.Ativar]
        };

        return map.SelectMany(pair => pair.Value.Select(action =>
            new PermissionCatalogItem(Key(pair.Key, action), pair.Key, action, Description(pair.Key, action), -1))).ToArray();
    }

    private static string Description(string domain, string action)
        => $"{Label(action)} {Label(domain)}";

    private static string Label(string value)
        => value switch
        {
            "paineis" => "painéis",
            "variaveis" => "variáveis",
            "formulas" => "fórmulas",
            "referencias" => "referências",
            "relatorios" => "relatórios",
            "usuarios" => "usuários",
            "configuracoes" => "configurações",
            "aprovacao-conteudo" => "aprovação de conteúdo",
            "criar" => "Criar",
            "editar" => "Editar",
            "excluir" => "Excluir",
            "importar" => "Importar",
            "exportar" => "Exportar",
            "aprovar" => "Aprovar",
            "vincular" => "Vincular",
            "ativar" => "Ativar/inativar",
            "visualizar" => "Visualizar",
            _ => value
        };
}

public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string dominio, string acao)
        : base(typeof(PermissionAuthorizationFilter))
    {
        Arguments = [dominio, acao];
    }
}

public sealed class PermissionAuthorizationFilter(string dominio, string acao, IPermissionService permissions)
    : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
            || context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null) return;
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Fail("Nao autenticado", "Faça login para continuar.", 401));
            return;
        }

        var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var perfil = user.FindFirstValue(ClaimTypes.Role) ?? "";
        if (!int.TryParse(idValue, out var codUsuario)
            || !await permissions.HasPermissionAsync(codUsuario, perfil, dominio, acao, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(ApiResponse.Fail("Acesso negado", "Usuario sem permissao para esta acao.", 403))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}

[ApiController]
[Route("api/web/permissoes")]
[Authorize(Roles = "admin")]
public sealed class PermissionsController(IPermissionService permissions) : ControllerBase
{
    [HttpGet("catalogo")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Catalogo(CancellationToken ct)
        => Ok(ApiResponse.Ok(await permissions.GetCatalogAsync(ct)));

    [HttpGet("perfis")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Perfis(CancellationToken ct)
        => Ok(ApiResponse.Ok(await permissions.ListProfilesAsync(ct)));

    [HttpGet("perfis/{perfil}")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Perfil(string perfil, CancellationToken ct)
        => Ok(ApiResponse.Ok(await permissions.GetProfileAsync(perfil, ct)));

    [HttpPut("perfis/{perfil}")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Editar)]
    public async Task<IActionResult> AtualizarPerfil(string perfil, UpdateProfilePermissionsRequest request, CancellationToken ct)
    {
        await permissions.SetProfileAsync(perfil, request.Permissoes ?? [], ct);
        return Ok(ApiResponse.OkMessage("Permissoes do perfil atualizadas."));
    }

    [HttpGet("usuarios/{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Usuario(int codUsuario, CancellationToken ct)
        => Ok(ApiResponse.Ok(await permissions.GetUserAsync(codUsuario, ct)));

    [HttpPut("usuarios/{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Configuracoes, PermissionActions.Editar)]
    public async Task<IActionResult> AtualizarUsuario(int codUsuario, UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        await permissions.SetUserAsync(codUsuario, request.Excecoes ?? [], ct);
        return Ok(ApiResponse.OkMessage("Permissoes do usuario atualizadas."));
    }
}
