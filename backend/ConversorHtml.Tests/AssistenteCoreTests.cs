using System.Reflection;

namespace ConversorHtml.Tests;

public sealed class AssistenteCoreTests
{
    private static readonly Assembly ApiAssembly = LoadApiAssembly();

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

    private static Type GetType(string name) => ApiAssembly.GetType(name, throwOnError: true)!;

    private static Assembly LoadApiAssembly()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "MdwConteudos.Api"))) root = root.Parent;
        var apiRoot = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Diretório backend não encontrado."),
            "MdwConteudos.Api", "bin");
        var path = Directory.EnumerateFiles(apiRoot, "MdwConteudos.Api.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? throw new FileNotFoundException("Assembly MdwConteudos.Api não encontrado.");
        return Assembly.LoadFrom(path);
    }
}
