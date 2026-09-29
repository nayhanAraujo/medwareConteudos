using System.Reflection;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Configuration;

namespace ConversorHtml.Tests;

public sealed class AssistenteCoreTests
{
    private static readonly Assembly ApiAssembly = typeof(MdwConteudos.Api.Configuration.AssistantFirebirdOptions).Assembly;

    [Fact]
    public void Codec_PreservaTextoIso88591EmRoundTripBase64()
    {
        var codec = GetType("MdwConteudos.Api.Modules.Assistente.Core.AssistantContentCodec");
        const string original = "Laudo: ação, coração e café";
        var encoded = (string)codec.GetMethod("EncodeTextToBase64")!.Invoke(null, [original])!;
        var decoded = (string)codec.GetMethod("DecodeBase64ToText")!.Invoke(null, [encoded])!;
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Paginacao_NormalizaLimitesECalculaOffset()
    {
        var type = GetType("MdwConteudos.Api.Modules.Assistente.Core.AssistantPageRequest");
        var request = Activator.CreateInstance(type, 3, 999, "  termo  ")!;
        Assert.Equal(200, type.GetProperty("NormalizedPageSize")!.GetValue(request));
        Assert.Equal(400, type.GetProperty("Offset")!.GetValue(request));
        Assert.Equal("termo", type.GetProperty("NormalizedSearch")!.GetValue(request));
    }

    [Fact]
    public void Opcoes_NaoExpõemSenhaAoConverterParaTexto()
    {
        var type = GetType("MdwConteudos.Api.Configuration.AssistantFirebirdOptions");
        var options = Activator.CreateInstance(type)!;
        type.GetProperty("Password")!.SetValue(options, "segredo-super-secreto");
        var text = options.ToString()!;
        Assert.DoesNotContain("segredo-super-secreto", text);
        Assert.Contains("Password = ***", text);
    }

    [Fact]
    public void Opcoes_IncluemClienteNativoQuandoConfigurado()
    {
        var library = Path.GetTempFileName();
        var database = Path.GetTempFileName();
        try
        {
            var options = new AssistantFirebirdOptions
            {
                Database = database,
                Password = "segredo",
                ClientLibrary = library
            };

            var parsed = new FbConnectionStringBuilder(options.BuildConnectionString());

            Assert.Equal(Path.GetFullPath(library), parsed.ClientLibrary);
            Assert.Equal(FbServerType.Embedded, parsed.ServerType);
        }
        finally
        {
            File.Delete(library);
            File.Delete(database);
        }
    }

    private static Type GetType(string name) => ApiAssembly.GetType(name, throwOnError: true)!;

}
