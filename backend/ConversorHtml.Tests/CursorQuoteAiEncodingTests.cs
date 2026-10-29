using System.Text;
using System.Text.Json;
using ConversorHtml.Application.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

[CollectionDefinition("AI console encoding", DisableParallelization = true)]
public sealed class AiConsoleEncodingCollection { }

[Collection("AI console encoding")]
public sealed class CursorQuoteAiEncodingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Utf8BridgeOutputIsPreservedWithWesternWindowsConsoleEncoding(bool malformed)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var previous=Console.OutputEncoding;
        var directory=Directory.CreateTempSubdirectory("quote-encoding-test-");
        try
        {
            Console.OutputEncoding=Encoding.GetEncoding(1252);
            var script=Path.Combine(directory.FullName,"fake.mjs");
            var suggestion=JsonSerializer.Serialize(OrcamentoJson.Write(OrcamentoAiTests.Suggestion()));
            await File.WriteAllTextAsync(script,"""
                process.stdin.setEncoding('utf8');
                let raw=''; for await (const chunk of process.stdin) raw+=chunk;
                const input=JSON.parse(raw);
                """+ $"\nconst output=JSON.parse({suggestion});\n" + """
                output.summary=input.briefing;
                output.items[0].description=input.title;
                if(input.title==='invalid') process.stdout.write(Buffer.from([0xff]));
                else process.stdout.write(JSON.stringify(output));
                """,new UTF8Encoding(false));
            var provider=new CursorQuoteAiProvider(
                Options.Create(new OrcamentoAiOptions {Enabled=true,NodePath=Environment.GetEnvironmentVariable("MDW_TEST_NODE_PATH")??"node",BridgePath=script}),
                Options.Create(new CursorOptions {ApiKey="TEST_ONLY_NO_NETWORK"}),new TestEnvironment {ContentRootPath=directory.FullName});
            const string briefing="Solicitação de migração — revisão técnica: clínica, impressão, inclusão, órgãos, ≥, Ω.";
            var input=new QuoteAiInput(malformed?"invalid":"Ajuste de laudo – coração","Solicitação","",[],[]) {Briefing=briefing};
            if(malformed)
            {
                var error=await Assert.ThrowsAsync<OrcamentoException>(()=>provider.Analyze(input,default));
                Assert.Equal(502,error.Status);Assert.Contains("codificação inválida",error.Message);
                return;
            }
            var result=await provider.Analyze(input,default);
            Assert.Equal(briefing,result.Summary);
            Assert.Equal("Ajuste de laudo – coração",result.Items[0].Description);
        }
        finally
        {
            Console.OutputEncoding=previous;
            directory.Delete(true);
        }
    }
    private sealed class TestEnvironment:IWebHostEnvironment
    {
        public string EnvironmentName {get;set;}="Development";
        public string ApplicationName {get;set;}="EncodingTest";
        public string ContentRootPath {get;set;}="";
        public string WebRootPath {get;set;}="";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
        public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
    }
}
