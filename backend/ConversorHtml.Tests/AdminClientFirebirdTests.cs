using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auditoria;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed partial class OrcamentoFirebirdTests
{
    private sealed class FakeAdminDirectory:IAdminClientDirectory
    {
        public AdminClientSnapshot? Value=new(13687,"Clínica oficial de teste","11222333000181",true);
        public int Calls;
        public Task<AdminClientSnapshot?> GetAsync(int code,CancellationToken ct){Calls++;return Task.FromResult(Value);}
    }
    private static ImportAdminClientRequest Approve(AdminClientPreview preview)=>new(preview.ConsultationId!.Value,true,"Código e CNPJ conferidos pelo responsável.");
    [FirebirdPublicationFact]
    public async Task CpfClientCanBeImportedAndRetriedWithoutDuplicateFiscalIdentity()
    {
        var provider=new FakeAdminDirectory {Value=new(3270,"Pessoa de teste","529.982.247-25",true)};
        var preview=await Service.PreviewAdminClient(new(3270),provider,default);
        Assert.True(preview.Resolution.CanImport);
        Assert.True((await Service.ImportAdminClient(Approve(preview),provider,default)).Created);
        Assert.False((await Service.ImportAdminClient(Approve(preview),provider,default)).Created);
        await OrcamentoSchema.MigrateAsync(factory);await OrcamentoSchema.MigrateAsync(factory);
        await using var c=await factory.OpenConnectionAsync();
        Assert.Equal("52998224725",await c.ExecuteScalarAsync<string>("SELECT DOCUMENTOFISCAL FROM CLIENTEADMIN WHERE CODCLIENTE=3270"));
        Assert.Null(await c.ExecuteScalarAsync<string?>("SELECT CNPJ FROM CLIENTEADMIN WHERE CODCLIENTE=3270"));
        Assert.Equal("encontrado_local",(await Service.ResolveAdminClient(new(3270),provider,default)).State);
        provider.Value=provider.Value! with {CodigoAdmin=3271,Name="Outro cadastro"};
        Assert.Equal("conflito",(await Service.ResolveAdminClient(new(3271),provider,default)).State);
    }
    [FirebirdPublicationFact]
    public async Task FiscalMigrationPreservesLegacyCnpjAndIsRepeatable()
    {
        await using var c=await factory.OpenConnectionAsync();
        await c.ExecuteAsync("ALTER TABLE CLIENTEADMIN DROP CONSTRAINT UNQ_CLIENTEADMIN_DOCFISCAL");
        await c.ExecuteAsync("ALTER TABLE CLIENTEADMIN DROP DOCUMENTOFISCAL");
        await c.ExecuteAsync("ALTER TABLE CLIENTEADMIN ALTER CNPJ SET NOT NULL");
        await c.ExecuteAsync("INSERT INTO CLIENTES VALUES(4498,'Cliente legado',1)");
        await c.ExecuteAsync("INSERT INTO CLIENTEADMIN(CODCLIENTE,CNPJ,DOCUMENTO,CODUSUARIO,DTHRCRIACAO) VALUES(4498,'11222333000181',@json,1,CURRENT_TIMESTAMP)",new {json=QuoteTextBlob.Json(new AdminClientSnapshot(4498,"Cliente legado","11222333000181",true))});
        var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"docs/sql/scripts/add-clienteadmin-documentofiscal.sql")))dir=dir.Parent;Assert.NotNull(dir);
        var sql=File.ReadAllText(Path.Combine(dir.FullName,"docs/sql/scripts/add-clienteadmin-documentofiscal.sql"));
        for(var attempt=0;attempt<2;attempt++)
            foreach(Match match in Regex.Matches(sql,@"EXECUTE BLOCK AS[\s\S]*?END\^"))
                await c.ExecuteAsync(match.Value.TrimEnd('^'));
        await OrcamentoSchema.MigrateAsync(factory);
        Assert.Equal("11222333000181",await c.ExecuteScalarAsync<string>("SELECT DOCUMENTOFISCAL FROM CLIENTEADMIN WHERE CODCLIENTE=4498"));
        Assert.Equal("11222333000181",(await c.ExecuteScalarAsync<string>("SELECT CNPJ FROM CLIENTEADMIN WHERE CODCLIENTE=4498")).Trim());
    }
    [FirebirdPublicationFact]
    public async Task ClientAdminImportRechecksOfficialDataPreservesExternalCodeAndIsIdempotent()
    {
        var provider=new FakeAdminDirectory();provider.Value=provider.Value! with {Cnpj="11.222.333/0001-81"};var proposalId=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);var frozen=OrcamentoJson.Write((await Service.Get(proposalId,default)).Snapshot);
        var resolution=await Service.ResolveAdminClient(new(13687),provider,default);Assert.Equal("encontrado_admin",resolution.State);Assert.True(resolution.CanImport);
        var preview=await Service.PreviewAdminClient(new(13687),provider,default);Context(2);
        Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ImportAdminClient(Approve(preview),provider,default))).Status);Context(1);
        var result=await Service.ImportAdminClient(Approve(preview),provider,default);Assert.True(result.Created);Assert.Equal(13687,result.Id);Assert.False((await Service.ImportAdminClient(Approve(preview),provider,default)).Created);
        var byCnpj=await Service.ResolveAdminClient(new(Cnpj:"11.222.333/0001-81"),provider,default);Assert.Equal("encontrado_local",byCnpj.State);Assert.Equal(13687,byCnpj.LocalId);
        var localCadastros=new MdwConteudos.Api.Modules.PaineisCadastros.PaineisCadastrosService(factory);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>localCadastros.DeleteAsync(MdwConteudos.Api.Modules.PaineisCadastros.PainelCadastroTipo.Cliente,13687,default));
        Assert.Equal("encontrado_local",(await Service.ResolveAdminClient(new(13687),provider,default)).State);
        Assert.Equal(frozen,OrcamentoJson.Write((await Service.Get(proposalId,default)).Snapshot));
        await using var c=await factory.OpenConnectionAsync();Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTES WHERE CODCLIENTE=13687"));Assert.Equal(0,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTECOMERCIAL WHERE CODCLIENTE=13687"));
        Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AUDITORIA_EVENTO WHERE ACTION='importar_cliente_admin'"));
        var summary=await c.ExecuteScalarAsync<string>("SELECT SUMMARY FROM AUDITORIA_EVENTO WHERE ACTION='importar_cliente_admin'");Assert.DoesNotContain("11222333",summary);Assert.DoesNotContain("Clínica oficial",summary);
    }
    [FirebirdPublicationFact]
    public async Task AdminClientConflictsDoNotOverwriteMergeOrAdoptUnprovedLegacyIdentity()
    {
        var provider=new FakeAdminDirectory();await using var c=await factory.OpenConnectionAsync();
        await c.ExecuteAsync("INSERT INTO CLIENTES VALUES(13687,'Clínica oficial de teste',1)");
        Assert.Equal("conflito",(await Service.ResolveAdminClient(new(13687),provider,default)).State);Assert.Null((await Service.PreviewAdminClient(new(13687),provider,default)).ConsultationId);
        await Service.SaveClient(13687,new("0",new(FiscalId:"11.222.333/0001-81",Email:"existing@example.com",MonthlyFee:500)),default);
        var before=await c.ExecuteScalarAsync<string>("SELECT DOCUMENTO FROM CLIENTECOMERCIAL WHERE CODCLIENTE=13687");
        var preview=await Service.PreviewAdminClient(new(13687),provider,default);Assert.True(preview.Resolution.CanImport);Assert.False((await Service.ImportAdminClient(Approve(preview),provider,default)).Created);
        Assert.Equal(before,await c.ExecuteScalarAsync<string>("SELECT DOCUMENTO FROM CLIENTECOMERCIAL WHERE CODCLIENTE=13687"));
        provider.Value=provider.Value! with {CodigoAdmin=14000};Assert.Equal("conflito",(await Service.ResolveAdminClient(new(14000),provider,default)).State);
        provider.Value=provider.Value! with {CodigoAdmin=13687,Name="Outro nome oficial"};Assert.Equal("conflito",(await Service.ResolveAdminClient(new(13687),provider,default)).State);
        Assert.Equal("Clínica oficial de teste",await c.ExecuteScalarAsync<string>("SELECT NOME FROM CLIENTES WHERE CODCLIENTE=13687"));
    }
    [FirebirdPublicationFact]
    public async Task ExpiredOrChangedOfficialPreviewCannotCreateClient()
    {
        var provider=new FakeAdminDirectory();var preview=await Service.PreviewAdminClient(new(13687),provider,default);
        provider.Value=provider.Value! with {Name="Cadastro atualizado"};Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ImportAdminClient(Approve(preview),provider,default))).Status);
        preview=await Service.PreviewAdminClient(new(13687),provider,default);await using var c=await factory.OpenConnectionAsync();await c.ExecuteAsync("UPDATE CLIENTEADMINCONSULTA SET DTHREXPIRACAO=DATEADD(-1 HOUR TO CURRENT_TIMESTAMP)");
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ImportAdminClient(Approve(preview),provider,default))).Status);Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTES"));
        provider.Value=provider.Value! with {Active=false};Assert.Equal("inativo",(await Service.PreviewAdminClient(new(13687),provider,default)).Resolution.State);
    }
    [FirebirdPublicationFact]
    public async Task TwoApprovedPreviewsRaceWithoutDuplicateClientsOrOrphanMapping()
    {
        var provider=new FakeAdminDirectory();var first=await Service.PreviewAdminClient(new(13687),provider,default);var second=await Service.PreviewAdminClient(new(13687),provider,default);
        async Task TryImport(AdminClientPreview p){try{await Service.ImportAdminClient(Approve(p),provider,default);}catch(FbException){/* unique/deadlock rejects loser; retry is safe */}catch(OrcamentoException e)when(e.Status==409){}}
        await Task.WhenAll(TryImport(first),TryImport(second));
        Assert.False((await Service.ImportAdminClient(Approve(first),provider,default)).Created);Assert.False((await Service.ImportAdminClient(Approve(second),provider,default)).Created);
        await using var c=await factory.OpenConnectionAsync();Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTES WHERE CODCLIENTE=13687"));Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTEADMIN"));
    }
    [FirebirdPublicationFact]
    public async Task TriggerChangingExternalCodeRollsBack()
    {
        var provider=new FakeAdminDirectory();await using var c=await factory.OpenConnectionAsync();
        await c.ExecuteAsync("CREATE TRIGGER CLIENTES_TEST_ID FOR CLIENTES ACTIVE BEFORE INSERT AS BEGIN NEW.CODCLIENTE=777; END");
        var preview=await Service.PreviewAdminClient(new(13687),provider,default);Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ImportAdminClient(Approve(preview),provider,default))).Status);
        Assert.Equal(0,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTES WHERE CODCLIENTE IN (777,13687)"));Assert.Equal(0,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTEADMIN"));await c.ExecuteAsync("DROP TRIGGER CLIENTES_TEST_ID");
    }
    [FirebirdPublicationFact]
    public async Task AdditiveAdminScriptIsRepeatableAndPreservesExistingQuote()
    {
        var id=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);var before=OrcamentoJson.Write((await Service.Get(id,default)).Snapshot);
        await using var c=await factory.OpenConnectionAsync();await c.ExecuteAsync("DROP TABLE CLIENTEADMINCONSULTA");await c.ExecuteAsync("DROP TABLE CLIENTEADMIN");
        var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!System.IO.File.Exists(Path.Combine(dir.FullName,"docs/sql/scripts/add-clienteadmin.sql")))dir=dir.Parent;Assert.NotNull(dir);
        var sql=System.IO.File.ReadAllText(Path.Combine(dir.FullName,"docs/sql/scripts/add-clienteadmin.sql"));var block=Regex.Match(sql,@"EXECUTE BLOCK AS[\s\S]*?END\^").Value.TrimEnd('^');await c.ExecuteAsync(block);await c.ExecuteAsync(block);
        await OrcamentoSchema.MigrateAsync(factory);await OrcamentoSchema.MigrateAsync(factory);Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLIENTES"));Assert.Equal(before,OrcamentoJson.Write((await Service.Get(id,default)).Snapshot));
    }
    [FirebirdPublicationFact]
    public async Task AlwaysAndByDefaultIdentitiesPreserveAdminCodeWithoutResettingTheirSequences()
    {
        foreach(var identity in new[]{"ALWAYS","BY DEFAULT"})
        {
            var connection=new FbConnectionStringBuilder(cs){Database=Path.Combine(Path.GetTempPath(),$"admin-identity-{Guid.NewGuid():N}.fdb")}.ToString();FbConnection.CreateDatabase(connection);
            Exception? originalFailure=null;
            try
            {
                var isolated=new Factory(connection,accessor);
                await using(var c=new FbConnection(connection))
                {
                    await c.OpenAsync();
                    await c.ExecuteAsync("CREATE TABLE USUARIO(CODUSUARIO INTEGER PRIMARY KEY,NOME VARCHAR(100),PERFIL VARCHAR(20),STATUS INTEGER)");await c.ExecuteAsync("INSERT INTO USUARIO VALUES(1,'Responsável','admin',-1)");
                    await c.ExecuteAsync($"CREATE TABLE CLIENTES(CODCLIENTE INTEGER GENERATED {identity} AS IDENTITY (START WITH 100) PRIMARY KEY,NOME VARCHAR(100),STATUS INTEGER)");await c.ExecuteAsync("INSERT INTO CLIENTES(NOME,STATUS) VALUES('Existente',1)");
                }
                await new PermissionService(isolated).EnsureSchemaAsync();await OrcamentoSchema.MigrateAsync(isolated);await AuditSchema.MigrateAsync(isolated);Context(1);
                var service=new OrcamentoService(isolated,accessor,TimeProvider.System);var provider=new FakeAdminDirectory();var preview=await service.PreviewAdminClient(new(13687),provider,default);Assert.True((await service.ImportAdminClient(Approve(preview),provider,default)).Created);
                await using var verify=await isolated.OpenConnectionAsync();Assert.Equal(13687,await verify.ExecuteScalarAsync<int>("SELECT CODCLIENTE FROM CLIENTES WHERE NOME='Clínica oficial de teste'"));
                Assert.Equal(101,await verify.ExecuteScalarAsync<int>("INSERT INTO CLIENTES(NOME,STATUS) VALUES('Próximo local',1) RETURNING CODCLIENTE"));
            }
            catch(Exception ex){originalFailure=ex;throw;}
            finally{try{FbConnection.DropDatabase(connection);}catch(FbException)when(originalFailure is not null){/* Do not hide the actual assertion/DDL failure behind cleanup diagnostics. */}}
        }
    }
    [FirebirdPublicationFact]
    public async Task LimitedBotCanResolveButCannotIssuePreviewOrImportClients()
    {
        await using(var c=await factory.OpenConnectionAsync())await c.ExecuteAsync("INSERT INTO USUARIO_PERMISSAO(CODUSUARIO,CHAVE,MODO) VALUES(4,'orcamentos.consultar_clientes_admin','PERMITIR')");
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Services.AddSingleton<IHttpContextAccessor>(accessor);builder.Services.AddSingleton<IFirebirdConnectionFactory>(factory);builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddScoped<OrcamentoService>();builder.Services.AddScoped<AuditWriter>();builder.Services.AddPermissionsModule();builder.Services.AddSingleton<IAdminClientDirectory>(new FakeAdminDirectory());
        builder.Services.AddAuthentication("BudgetTest").AddScheme<AuthenticationSchemeOptions,TestAuthentication>("BudgetTest",_=>{});builder.Services.AddAuthorization();builder.Services.AddControllers().AddApplicationPart(typeof(OrcamentosController).Assembly);
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        try
        {
            using var client=new HttpClient{BaseAddress=new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/web/orcamentos/integracoes/emails/clientes/resolver",new ResolveAdminClientRequest(13687))).StatusCode);client.DefaultRequestHeaders.Add("X-Test-User","4");
            var response=await client.PostAsJsonAsync("/api/web/orcamentos/integracoes/emails/clientes/resolver",new ResolveAdminClientRequest(13687));response.EnsureSuccessStatusCode();Assert.DoesNotContain("consultationId",await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/web/orcamentos/clientes/admin/consultar",new AdminClientPreviewRequest(13687))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/web/orcamentos/clientes/importar-admin",new ImportAdminClientRequest(Guid.NewGuid(),true,"Não autorizado"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/web/paineis/cadastros/clientes",new{nome="Cliente inventado"})).StatusCode);
        }
        finally{await app.StopAsync();}
    }
}
