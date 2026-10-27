using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class AdminClientDirectoryTests
{
    [Theory]
    [InlineData("11.222.333/0001-81","11222333000181")]
    [InlineData("12.ABC.345/01DE-35","12ABC34501DE35")]
    public void CnpjSupportsNumericAndOfficialAlphanumericExample(string input,string expected)=>Assert.Equal(expected,AdminClientIdentity.Cnpj(input));
    [Theory]
    [InlineData("00000000000000")][InlineData("11222333000182")][InlineData("bad")][InlineData("12ABC34501DE00")][InlineData("11_222333000181")]
    public void InvalidCnpjCannotBecomeAnIdentity(string input)=>Assert.Throws<OrcamentoException>(()=>AdminClientIdentity.Cnpj(input));
    private static AdminDirectoryOptions Config()=>new(){Enabled=true,ContractConfirmed=true,Username="test-user",Password="TEST_SECRET",TokenPointer="/data/token",ClientPointer="/data",CodeField="codigo",NameField="razaoSocial",CnpjField="documento",ActiveField="status",ActiveValues=["ATIVO"],InactiveValues=["INATIVO"]};
    [Fact]
    public async Task LoginAndSingularClientRouteUseMappedContractAndNoSharedBearerHeader()
    {
        var handler=new Handler(async request=>{
            if(request.Method==HttpMethod.Post){Assert.Equal("/Auth/login",request.RequestUri!.AbsolutePath);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal("ADMIN",json.RootElement.GetProperty("registroAplicacao").GetString());Assert.Equal("test-user",json.RootElement.GetProperty("usuario").GetString());Assert.Null(request.Headers.Authorization);return Json("""{"data":{"token":"TEST_TOKEN"}}""");}
            Assert.Equal("/cliente/13687",request.RequestUri!.AbsolutePath);Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);Assert.Equal("TEST_TOKEN",request.Headers.Authorization.Parameter);
            return Json("""{"data":{"codigo":13687,"razaoSocial":"Clínica oficial de teste","documento":"11.222.333/0001-81","status":"ATIVO","mensalidade":99999,"email":"private@example.com"}}""");
        });
        using var http=new HttpClient(handler);var service=new AdminClientDirectory(http,Options.Create(Config()));
        var value=await service.GetAsync(13687,default);Assert.Equal(new AdminClientSnapshot(13687,"Clínica oficial de teste","11222333000181",true),value);Assert.Equal(2,handler.Calls);Assert.Null(http.DefaultRequestHeaders.Authorization);
    }
    [Fact]
    public async Task DisabledOrUnconfirmedContractAndUntrustedRouteDoNotSendCredentials()
    {
        var handler=new Handler(_=>throw new Exception("Network must not be used"));using var http=new HttpClient(handler);var o=Config();o.ContractConfirmed=false;
        Assert.Equal(503,(await Assert.ThrowsAsync<OrcamentoException>(()=>new AdminClientDirectory(http,Options.Create(o)).GetAsync(13687,default))).Status);
        o.ContractConfirmed=true;o.ClientPath="https://other.example.com/cliente/{codigoAdmin}";
        Assert.Equal(503,(await Assert.ThrowsAsync<OrcamentoException>(()=>new AdminClientDirectory(http,Options.Create(o)).GetAsync(13687,default))).Status);Assert.Equal(0,handler.Calls);
    }
    [Theory]
    [InlineData("{\"data\":{\"codigo\":99,\"razaoSocial\":\"Clínica\",\"documento\":\"11222333000181\",\"status\":\"ATIVO\"}}")]
    [InlineData("{\"data\":{\"codigo\":13687,\"razaoSocial\":\"Clínica\",\"documento\":\"INVALID\",\"status\":\"ATIVO\"}}")]
    [InlineData("{\"data\":{\"codigo\":13687,\"razaoSocial\":\"Clínica\",\"documento\":\"11222333000181\",\"status\":\"UNKNOWN\"}}")]
    [InlineData("{\"unexpected\":\"TEST_SECRET\"}")]
    public async Task IncompatibleRemotePayloadNeverImportsOrLeaksItsBody(string payload)
    {
        var handler=new Handler(r=>Task.FromResult(Json(r.Method==HttpMethod.Post?"""{"data":{"token":"TEST_TOKEN"}}""":payload)));
        using var http=new HttpClient(handler);var error=await Assert.ThrowsAsync<OrcamentoException>(()=>new AdminClientDirectory(http,Options.Create(Config())).GetAsync(13687,default));Assert.Equal(502,error.Status);Assert.DoesNotContain("TEST_SECRET",error.Message);Assert.DoesNotContain("TEST_TOKEN",error.Message);
    }
    [Fact]
    public async Task RedirectAndAuthenticationFailureAreNotRetried()
    {
        var handler=new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://other.example.com/")}}));using var http=new HttpClient(handler);
        Assert.Equal(503,(await Assert.ThrowsAsync<OrcamentoException>(()=>new AdminClientDirectory(http,Options.Create(Config())).GetAsync(13687,default))).Status);Assert.Equal(1,handler.Calls);
    }
    private static HttpResponseMessage Json(string text)=>new(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> response):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;return response(request);}
    }
}
