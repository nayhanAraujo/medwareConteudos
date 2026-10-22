using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Modules.Auditoria;

namespace MdwConteudos.Api.Infrastructure;

public interface IFirebirdConnectionFactory
{
    Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default);
}

public class FirebirdConnectionFactory : IFirebirdConnectionFactory
{
    private readonly FirebirdOptions _options;
    private readonly ILogger<FirebirdConnectionFactory> _logger;
    private readonly IHttpContextAccessor? _http;

    public FirebirdConnectionFactory(IOptions<FirebirdOptions> options, ILogger<FirebirdConnectionFactory> logger, IHttpContextAccessor? http = null)
    {
        _options = options.Value;
        _logger = logger;
        _http = http;
    }

    public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new FbConnection(_options.BuildConnectionString("UTF8"));
        try
        {
            await conn.OpenAsync(ct);
        }
        catch
        {
            await conn.DisposeAsync();
            ct.ThrowIfCancellationRequested();
            _logger.LogWarning("Falha ao conectar REFERENCIAS com UTF8, tentando ISO8859_1.");
            var fallback = new FbConnection(_options.BuildConnectionString("ISO8859_1"));
            try { await fallback.OpenAsync(ct); conn = fallback; }
            catch { await fallback.DisposeAsync(); throw; }
        }
        try { await AuditSession.InitializeAsync(conn, _http?.HttpContext, ct); return conn; }
        catch { await conn.DisposeAsync(); throw; }
    }
}
