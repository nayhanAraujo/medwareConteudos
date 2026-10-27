using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class AdminDirectoryOptions
{
    public bool Enabled { get; set; }
    // Deliberate deployment gate: field mappings/auth response must be checked against the real contract.
    public bool ContractConfirmed { get; set; }
    public string BaseUrl { get; set; } = "https://apiv2.medware.com.br/";
    public string LoginPath { get; set; } = "Auth/login";
    public string ClientPath { get; set; } = "cliente/{codigoAdmin}";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string TokenPointer { get; set; } = "/token";
    public string ClientPointer { get; set; } = "";
    public string CodeField { get; set; } = "codigoAdmin";
    public string NameField { get; set; } = "nome";
    public string CnpjField { get; set; } = "cnpj";
    public string ActiveField { get; set; } = "ativo";
    public string[] ActiveValues { get; set; } = ["true", "1"];
    public string[] InactiveValues { get; set; } = ["false", "0"];
}
public sealed record AdminClientSnapshot(int CodigoAdmin, string Name, string Cnpj, bool Active);
public interface IAdminClientDirectory { Task<AdminClientSnapshot?> GetAsync(int codigoAdmin, CancellationToken ct); }

public static class AdminClientIdentity
{
    public static string Cnpj(string? value)
    {
        var s=(value??"").Trim().ToUpperInvariant().Replace(".","").Replace("/","").Replace("-","");
        if(s.Length!=14 || s.Take(12).Any(c=>!(c is >= '0' and <= '9' or >= 'A' and <= 'Z')) || s.Skip(12).Any(c=>c is < '0' or > '9') || s.Distinct().Count()==1)
            throw new OrcamentoException(422,"CNPJ inválido. Informe o documento completo com dígitos verificadores.");
        static int Digit(string text,int[] weights){var sum=text.Select((c,i)=>(c-'0')*weights[i]).Sum();var mod=sum%11;return mod<2?0:11-mod;}
        if(Digit(s[..12],[5,4,3,2,9,8,7,6,5,4,3,2])!=s[12]-'0'||Digit(s[..13],[6,5,4,3,2,9,8,7,6,5,4,3,2])!=s[13]-'0')
            throw new OrcamentoException(422,"Dígitos verificadores do CNPJ inválidos.");
        return s;
    }
    public static string? TryCnpj(string? value){try{return Cnpj(value);}catch(OrcamentoException){return null;}}
    public static AdminClientSnapshot Validate(AdminClientSnapshot value)
    {
        if(value.CodigoAdmin<=0||string.IsNullOrWhiteSpace(value.Name))throw new OrcamentoException(502,"Cadastro Admin sem código ou nome válido.");
        QuoteCalculator.CheckText(value.Name,"Nome oficial",300);
        return value with {Name=value.Name.Trim(),Cnpj=Cnpj(value.Cnpj)};
    }
    public static string NameKey(string value)=>value.Trim().Normalize().ToUpperInvariant();
}

public sealed class AdminClientDirectory(HttpClient http,IOptions<AdminDirectoryOptions> settings):IAdminClientDirectory
{
    public async Task<AdminClientSnapshot?> GetAsync(int codigoAdmin,CancellationToken ct)
    {
        if(codigoAdmin<=0)throw new OrcamentoException(422,"Informe um código Admin positivo.");
        var o=settings.Value;
        if(!o.Enabled||!o.ContractConfirmed||string.IsNullOrWhiteSpace(o.Username)||string.IsNullOrWhiteSpace(o.Password))
            throw new OrcamentoException(503,"Integração Admin indisponível: configure conta técnica e confirme o contrato remoto antes de habilitar.");
        if(!Uri.TryCreate(o.BaseUrl,UriKind.Absolute,out var root)||root.Scheme!="https"||!string.IsNullOrEmpty(root.UserInfo)||!string.IsNullOrEmpty(root.Query)||!string.IsNullOrEmpty(root.Fragment))
            throw new OrcamentoException(503,"Base HTTPS da integração Admin inválida.");
        Uri Target(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||Uri.TryCreate(path,UriKind.Absolute,out _)||path.StartsWith('/')||path.Contains('\\')||!Uri.TryCreate(root,path,out var uri)||uri.Authority!=root.Authority||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.Fragment))
                throw new OrcamentoException(503,"Rota Admin inválida na configuração do servidor.");
            return uri;
        }
        if(string.IsNullOrWhiteSpace(o.ClientPath)||!o.ClientPath.Contains("{codigoAdmin}",StringComparison.Ordinal)||o.ActiveValues is not {Length:>0}||o.InactiveValues is not {Length:>0}||o.ActiveValues.Intersect(o.InactiveValues).Any()
            ||new[]{o.CodeField,o.NameField,o.CnpjField,o.ActiveField}.Any(string.IsNullOrWhiteSpace)||o.TokenPointer is null||o.ClientPointer is null)
            throw new OrcamentoException(503,"Mapeamento do contrato Admin incompleto.");
        var loginUri=Target(o.LoginPath);var clientUri=Target(o.ClientPath.Replace("{codigoAdmin}",codigoAdmin.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var login=new HttpRequestMessage(HttpMethod.Post,loginUri) {Content=JsonContent.Create(new {usuario=o.Username,senha=o.Password,registroAplicacao="ADMIN"})};
            using var auth=await http.SendAsync(login,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(!auth.IsSuccessStatusCode)throw new OrcamentoException(503,"A API Admin não autorizou a integração. Verifique a conta técnica e o contrato de login.");
            using var authJson=await Read(auth,timeout.Token);
            var token=Pointer(authJson.RootElement,o.TokenPointer);
            if(token.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(token.GetString())||token.GetString()!.Length>16384||token.GetString()!.Any(char.IsControl))throw new OrcamentoException(502,"Resposta de login Admin incompatível com o mapeamento configurado.");
            using var request=new HttpRequestMessage(HttpMethod.Get,clientUri);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.GetString());
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(response.StatusCode==HttpStatusCode.NotFound)return null;
            if(!response.IsSuccessStatusCode)throw new OrcamentoException(503,"Não foi possível consultar o cliente na API Admin.");
            using var json=await Read(response,timeout.Token);var item=Pointer(json.RootElement,o.ClientPointer);
            JsonElement Field(string name)=>item.TryGetProperty(name,out var v)?v:throw new JsonException();
            var code=Field(o.CodeField);var rawCode=code.ValueKind==JsonValueKind.String?code.GetString():code.GetRawText();
            if(!int.TryParse(rawCode,NumberStyles.None,CultureInfo.InvariantCulture,out var found)||found!=codigoAdmin)throw new JsonException();
            var active=Field(o.ActiveField);var flag=active.ValueKind==JsonValueKind.String?active.GetString()!:active.GetRawText();
            if(!o.ActiveValues.Contains(flag)&&!o.InactiveValues.Contains(flag))throw new JsonException();
            return AdminClientIdentity.Validate(new(found,Field(o.NameField).GetString()!,Field(o.CnpjField).GetString()!,o.ActiveValues.Contains(flag)));
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new OrcamentoException(503,"Tempo de consulta à API Admin esgotado. Nenhum cliente foi importado.");}
        catch(HttpRequestException){throw new OrcamentoException(503,"API Admin indisponível. Nenhum cliente foi importado.");}
        catch(OrcamentoException ex) when(ex.Status==422){throw new OrcamentoException(502,"Dados de identidade inválidos na resposta Admin; nenhum cadastro foi importado.");}
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException){throw new OrcamentoException(502,"Resposta Admin incompatível. Confira o mapeamento dos campos; nenhum dado foi importado.");}
    }
    private static JsonElement Pointer(JsonElement root,string pointer)
    {
        if(pointer=="")return root;
        if(!pointer.StartsWith('/'))throw new JsonException();
        foreach(var part in pointer[1..].Split('/'))root=root.GetProperty(part.Replace("~1","/").Replace("~0","~"));
        return root;
    }
    private static async Task<JsonDocument> Read(HttpResponseMessage response,CancellationToken ct)
    {
        const int max=1024*1024;if(response.Content.Headers.ContentLength>max)throw new JsonException();
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var bytes=new byte[8192];int count;
        while((count=await input.ReadAsync(bytes,ct))>0){if(output.Length+count>max)throw new JsonException();await output.WriteAsync(bytes.AsMemory(0,count),ct);}
        return JsonDocument.Parse(output.ToArray(),new JsonDocumentOptions{MaxDepth=32});
    }
}
