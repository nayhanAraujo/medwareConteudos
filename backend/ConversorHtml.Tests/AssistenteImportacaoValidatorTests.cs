using System.Reflection;
using System.Text;

namespace ConversorHtml.Tests;

public sealed class AssistenteImportacaoValidatorTests
{
    [Fact]
    public void Tipo1_ArmazenaBytesEmBase64()
    {
        var bytes = new byte[] { 0, 1, 2, 255 };
        var stored = PrepareScript(1, "legado.bin", bytes);
        Assert.Equal(bytes, Convert.FromBase64String(stored));
    }

    [Fact]
    public void Tipo2_ValidaMzEPeESuportaRoundTrip()
    {
        var bytes = new byte[132];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BitConverter.GetBytes(128).CopyTo(bytes, 0x3c);
        bytes[128] = (byte)'P'; bytes[129] = (byte)'E';
        var stored = PrepareScript(2, "modelo.dll", bytes);
        Assert.Equal(bytes, Convert.FromBase64String(stored));
    }

    [Fact]
    public void Tipo2_RejeitaDllSemAssinaturaPe()
    {
        var bytes = new byte[128];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        AssertValidatorError(() => PrepareScript(2, "modelo.dll", bytes), "PE");
    }

    [Fact]
    public void Tipo3_PreservaJsonBrutoERejeitaJsonInvalido()
    {
        const string json = "{\n  \"nome\": \"laudo\"\n}";
        Assert.Equal(json, PrepareScript(3, "modelo.json", Encoding.UTF8.GetBytes(json)));
        AssertValidatorError(() => PrepareScript(3, "modelo.json", "{"u8.ToArray()), "JSON");
    }

    [Fact]
    public void Mrd_ExigeExtensaoECabecalhoESuportaRoundTrip()
    {
        var bytes = Encoding.ASCII.GetBytes("Medware Designer Report 1.0\r\nconteudo");
        var stored = Invoke<string>("PrepareMrd", "modelo.mrd", bytes);
        Assert.Equal(bytes, Convert.FromBase64String(stored));
        AssertValidatorError(() => Invoke<string>("PrepareMrd", "modelo.txt", bytes), ".mrd");
        AssertValidatorError(() => Invoke<string>("PrepareMrd", "modelo.mrd", "invalido"u8.ToArray()), "cabeçalho");
    }

    private static string PrepareScript(short type, string name, byte[] bytes) => Invoke<string>("PrepareScript", type, name, bytes);

    private static T Invoke<T>(string method, params object[] args)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Release";
        var assemblyPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "MdwConteudos.Api", "bin", configuration, "net10.0", "MdwConteudos.Api.dll"));
        if (!File.Exists(assemblyPath))
            assemblyPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "MdwConteudos.Api", "bin", "Release", "net10.0", "MdwConteudos.Api.dll"));
        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType("MdwConteudos.Api.Modules.Assistente.Importacao.AssistenteImportacaoValidator", true)!;
        try { return (T)type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args)!; }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
    }

    private static void AssertValidatorError(Action action, string expected)
    {
        var error = Assert.ThrowsAny<InvalidOperationException>(action);
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
