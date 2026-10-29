using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Modules.Auditoria;

namespace ConversorHtml.Tests;

public sealed class AuditTextTests
{
    [Theory]
    [InlineData("Robô — Orçamentos")]
    [InlineData("")]
    [InlineData(null)]
    public void AuditParametersDeclareUtf8IndependentlyOfConnection(string? value)
    {
        using var command=new FbCommand();
        new AuditText(value).AddParameter(command,"audit");
        var parameter=command.Parameters[0];
        Assert.Equal(FbCharset.Utf8,parameter.Charset);
        Assert.Equal(FbDbType.VarChar,parameter.FbDbType);
        Assert.Equal((object?)value??DBNull.Value,parameter.Value);
        Assert.True(parameter.Size>0);
    }
}
