using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace MdwConteudos.Api.Infrastructure;

// Dapper's generated string binding sets Size=-1 above 4000 characters.
// Firebird rejects that size; textual BLOBs must have an explicit provider type.
public sealed class FirebirdTextBlob(string? value) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name) =>
        command.Parameters.Add(new FbParameter(name, FbDbType.Text) { Value = (object?)value ?? DBNull.Value });
}
