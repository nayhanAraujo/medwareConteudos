using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;

namespace MdwConteudos.Api.Infrastructure;

public interface IFirebirdConnectionFactory
{
    Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default);
}

public class FirebirdConnectionFactory : IFirebirdConnectionFactory
{
    private readonly FirebirdOptions _options;
    private readonly ILogger<FirebirdConnectionFactory> _logger;

    public FirebirdConnectionFactory(IOptions<FirebirdOptions> options, ILogger<FirebirdConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new FbConnection(_options.BuildConnectionString("UTF8"));
        try
        {
            await conn.OpenAsync(ct);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            ct.ThrowIfCancellationRequested();
            _logger.LogWarning("Falha ao conectar REFERENCIAS com UTF8, tentando ISO8859_1.");
            var fallback = new FbConnection(_options.BuildConnectionString("ISO8859_1"));
            try { await fallback.OpenAsync(ct); return fallback; }
            catch { await fallback.DisposeAsync(); throw; }
        }
    }
}
