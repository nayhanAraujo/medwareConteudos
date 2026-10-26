using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace MdwConteudos.Api.Modules.Orcamentos;

// Dapper sets Size=-1 on strings longer than 4000 characters. Firebird rejects
// that size even when the destination column is a text BLOB. Bind its type directly.
public sealed class QuoteTextBlob(string? value) : SqlMapper.ICustomQueryParameter
{
    public static QuoteTextBlob Json<T>(T value) => new(OrcamentoJson.Write(value));
    public void AddParameter(IDbCommand command, string name) =>
        command.Parameters.Add(new FbParameter(name, FbDbType.Text) { Value = (object?)value ?? DBNull.Value });
}
