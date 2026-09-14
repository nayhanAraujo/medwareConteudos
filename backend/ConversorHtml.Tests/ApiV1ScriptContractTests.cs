using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MdwConteudos.Api.Modules.ApiPublica;

namespace ConversorHtml.Tests;

public class ApiV1ScriptContractTests
{
    [Fact]
    public void Real_zip_preserves_entry_names_spaces_accents_and_bytes()
    {
        var name = ApiV1Contract.ScriptBaseName(" Ecocárdio teste / 01 ", 42);
        Assert.Equal("Ecocárdio teste _ 01", name);
        var payload = Encoding.UTF8.GetBytes("{\"conteudo\":\"ação\"}");
        var result = ApiV1Contract.CreateScriptZip([(name + "_script.json", payload), (name + "_mrd.json", new byte[] { 1, 2, 3 })], name, "1.2", true);
        Assert.Equal(name + ".zip", result.FileDownloadName);
        using var archive = new ZipArchive(new MemoryStream(result.FileContents), ZipArchiveMode.Read);
        Assert.Equal(new[] { name + "_script.json", name + "_mrd.json" }, archive.Entries.Select(entry => entry.FullName).ToArray());
        using var content = new MemoryStream();
        using var entryStream = archive.Entries[0].Open();
        entryStream.CopyTo(content);
        Assert.Equal(payload, content.ToArray());
        Assert.Equal("script_42", ApiV1Contract.ScriptBaseName(null, 42));
    }

    [Theory]
    [InlineData(" Relatório / Teste ", "XML", "Relatório__Teste_.xml")]
    [InlineData(null, null, "relatorio_42.bin")]
    [InlineData("", "", "relatorio_42.bin")]
    public void Report_filenames_preserve_legacy_sanitization(string? name, string? format, string expected) =>
        Assert.Equal(expected, ApiV1Contract.RelatorioFileName(name, format, 42));

    [Theory]
    [InlineData(false, "SCRIPTLAUDO_MRD")]
    [InlineData(true, "SCRIPT_VERSAO_MRD")]
    public void Mrd_source_is_the_legacy_wire_name(bool active, string expected) =>
        Assert.Equal(expected, ApiV1Contract.MrdSource(active));

    [Fact]
    public async Task Actual_file_result_writes_legacy_source_header_and_download_filename()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        foreach (var fixture in ApiV1TestSupport.Reference.GetProperty("download_headers").EnumerateArray())
        {
            var active = fixture.GetProperty("active").GetBoolean();
            var result = new ScriptDownloadFileResult([1, 2, 3], "Ecocárdio teste.zip", fixture.GetProperty("version").GetString(), active);
            var expected = fixture.GetProperty("expected");
            Assert.Equal(expected.GetProperty("X-Script-Download-Source").GetString(), result.Source);
            Assert.Equal(expected.GetProperty("X-Script-Download-Suffix").GetString(), result.Suffix);
            Assert.Equal("Ecocárdio teste.zip", result.FileDownloadName);
            var context = new DefaultHttpContext { RequestServices = provider };
            context.Response.Body = new MemoryStream();
            await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));
            Assert.Equal(result.Source, context.Response.Headers["X-Script-Download-Source"].ToString());
            // Kestrel header values stay ASCII: Python's padrão becomes padrao on the wire.
            Assert.Equal(active ? result.Suffix : "padrao", context.Response.Headers["X-Script-Download-Suffix"].ToString());
            Assert.Equal("application/zip", context.Response.ContentType);
            Assert.Equal(3, context.Response.Body.Length);
            Assert.Contains("filename*=UTF-8''", context.Response.Headers.ContentDisposition.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", false)]
    [InlineData(" false ", false)]
    [InlineData("NO", false)]
    [InlineData("nao", false)]
    [InlineData("não", false)]
    [InlineData("1", true)]
    [InlineData("sim", true)]
    public void Include_files_filter_matches_Python(string? input, bool expected) =>
        Assert.Equal(expected, ApiScriptOperations.ParseIncluirArquivos(input));

    [Fact]
    public void Script_filters_are_trimmed_parameterized_and_ignore_invalid_values()
    {
        var (where, parameters) = ApiScriptOperations.BuildWhere(" Laudos UX ", " 1 ", " 0 ", " 42 ");
        Assert.Equal(4, where.Count);
        Assert.Equal("Laudos UX", parameters.Get<string>("sistema"));
        Assert.Equal(1, parameters.Get<int>("aprovado"));
        Assert.Equal(0, parameters.Get<int>("ativo"));
        Assert.Equal(42, parameters.Get<int>("pacote"));
        var invalid = ApiScriptOperations.BuildWhere("unknown", "true", "9", "x' OR 1=1 --");
        Assert.Equal(new[] { "1=1" }, invalid.Where);
        Assert.Empty(invalid.Params.ParameterNames);
        var latest = ApiScriptOperations.BuildWhere(null, null, null, null, ["s.DATA_VERIFICACAO IS NOT NULL"]);
        Assert.Equal(new[] { "s.DATA_VERIFICACAO IS NOT NULL" }, latest.Where);
    }
}
