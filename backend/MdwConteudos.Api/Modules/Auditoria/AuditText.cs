using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace MdwConteudos.Api.Modules.Auditoria;

// Audit columns are UTF8 even when the operational connection uses a legacy charset.
public sealed class AuditText(string? value) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name) =>
        command.Parameters.Add(new FbParameter(name, FbDbType.VarChar)
        {
            Charset = FbCharset.Utf8,
            Size = Math.Max(1, value?.Length ?? 0),
            Value = (object?)value ?? DBNull.Value
        });
}
