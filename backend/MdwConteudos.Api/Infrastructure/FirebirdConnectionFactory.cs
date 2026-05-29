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
        try
        {
            var conn = new FbConnection(_options.BuildConnectionString("UTF8"));
            await conn.OpenAsync(ct);
            return conn;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha UTF8, tentando ISO8859_1");
            var conn = new FbConnection(_options.BuildConnectionString("ISO8859_1"));
            await conn.OpenAsync(ct);
            return conn;
        }
    }
}
