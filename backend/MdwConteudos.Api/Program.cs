using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Infrastructure.Swagger;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.Assistente.Dominios;
using MdwConteudos.Api.Modules.Assistente.Importacao;
using MdwConteudos.Api.Modules.Assistente.Vinculos;
using MdwConteudos.Api.Modules.Auth;
using MdwConteudos.Api.Modules.Cadastros;
using MdwConteudos.Api.Modules.Dashboard;
using MdwConteudos.Api.Modules.FirebirdAdmin;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.PaineisCadastros;
using MdwConteudos.Api.Modules.PaineisComplementos;
using MdwConteudos.Api.Modules.RelatoriosComplementos;
using MdwConteudos.Api.Modules.Users;
using MdwConteudos.Api.Modules.Web;
using MdwConteudos.Api.Services;
using ConversorHtml.Application;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
// Override local por máquina (appsettings.*.local.json está no .gitignore)
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);
EnvFileLoader.LoadFromRepoRoot(builder.Configuration, builder.Environment.ContentRootPath);
const string DefaultWebJwtSecret = "mdw-web-dev-secret-change-me-2026-local-migration-only";

builder.Services.Configure<FirebirdOptions>(builder.Configuration.GetSection(FirebirdOptions.SectionName));
builder.Services.PostConfigure<FirebirdOptions>(opt =>
{
    opt.Database = FirstNonEmpty(
        Environment.GetEnvironmentVariable("FIREBIRD_DB"),
        opt.Database) ?? opt.Database;
    opt.Password = FirstNonEmpty(
        Environment.GetEnvironmentVariable("FIREBIRD_PASSWORD"),
        Environment.GetEnvironmentVariable("LOCAL_DB_PASSWORD"),
        opt.Password) ?? opt.Password;
    opt.Host = FirstNonEmpty(Environment.GetEnvironmentVariable("FIREBIRD_HOST"), opt.Host) ?? opt.Host;
    if (int.TryParse(Environment.GetEnvironmentVariable("FIREBIRD_PORT"), out var port))
        opt.Port = port;
    if (string.IsNullOrWhiteSpace(opt.Database))
    {
        var root = FindRepoRoot(builder.Environment.ContentRootPath);
        if (root != null)
            opt.Database = Path.Combine(root, "BD", "REFERENCIAS.FDB").Replace('\\', '/');
    }
    else if (!Path.IsPathFullyQualified(opt.Database))
    {
        opt.Database = Path.GetFullPath(Path.Combine(GetMigrationRoot(builder.Environment.ContentRootPath), opt.Database));
    }
});

builder.Services.Configure<AssistantFirebirdOptions>(builder.Configuration.GetSection(AssistantFirebirdOptions.SectionName));
builder.Services.PostConfigure<AssistantFirebirdOptions>(opt =>
{
    opt.ApplyEnvironmentVariables();
    if (!Path.IsPathFullyQualified(opt.Database))
    {
        opt.Database = Path.GetFullPath(Path.Combine(GetMigrationRoot(builder.Environment.ContentRootPath), opt.Database));
    }
});

builder.Services.Configure<WebAuthOptions>(builder.Configuration.GetSection(WebAuthOptions.SectionName));
builder.Services.PostConfigure<WebAuthOptions>(opt =>
{
    if (string.IsNullOrWhiteSpace(opt.JwtSecret))
        opt.JwtSecret = DefaultWebJwtSecret;
});

builder.Services.Configure<ApiPartnerOptions>(builder.Configuration.GetSection("ApiPartner"));
builder.Services.PostConfigure<ApiPartnerOptions>(opt =>
{
    opt.JwtSecret = FirstNonEmpty(Environment.GetEnvironmentVariable("API_JWT_SECRET"), opt.JwtSecret) ?? opt.JwtSecret;
    opt.JwtPassword = FirstNonEmpty(Environment.GetEnvironmentVariable("API_JWT_PASSWORD"), opt.JwtPassword) ?? opt.JwtPassword;
    if (double.TryParse(Environment.GetEnvironmentVariable("API_JWT_DATETIME_TOLERANCE_HOURS"), out var tol))
        opt.JwtDatetimeToleranceHours = tol;
});
builder.Services.Configure<LegacyPathsOptions>(builder.Configuration.GetSection("LegacyPaths"));

builder.Services.AddSingleton<IFirebirdConnectionFactory, FirebirdConnectionFactory>();
builder.Services.AddSingleton<IAssistantFirebirdConnectionFactory, AssistantFirebirdConnectionFactory>();
builder.Services.AddScoped<IAssistenteDominiosService, AssistenteDominiosService>();
builder.Services.AddScoped<IAssistenteImportacaoService, AssistenteImportacaoService>();
builder.Services.AddScoped<IAssistenteVinculosService, AssistenteVinculosService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IApiPublicaService, ApiPublicaService>();
builder.Services.AddScoped<ApiScriptOperations>();
builder.Services.AddScoped<IScriptsWebService, ScriptsWebService>();
builder.Services.AddScoped<IAgenteWebService, AgenteWebService>();
builder.Services.AddScoped<IConteudosWebService, ConteudosWebService>();
builder.Services.AddScoped<IPaineisWebService, PaineisWebService>();
builder.Services.AddScoped<IRelatoriosWebService, RelatoriosWebService>();
builder.Services.AddScoped<IVariaveisWebService, VariaveisWebService>();
builder.Services.AddScoped<IReferenciasService, ReferenciasService>();
builder.Services.AddScoped<IConversionAnalysisService, ConversionAnalysisService>();
builder.Services.AddScoped<ScriptsService>();
builder.Services.AddCadastrosModules();
builder.Services.AddDashboardModule();
builder.Services.AddFirebirdAdminModule();
builder.Services.AddFormulasModelosModules();
builder.Services.AddPaineisCadastros();
builder.Services.AddPaineisComplementos();
builder.Services.AddRelatoriosComplementos();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddRequestTimeouts();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(
        System.Text.Json.JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.SwaggerDoc(SwaggerDocPaths.Parceiros, new OpenApiInfo
    {
        Title = "MDW Conteúdos — API Parceiros",
        Version = "v1",
        Description =
            "Base **/apiconteudos/v1** com autenticação JWT.\n\n" +
            "**Como usar:**\n" +
            "1. Chame `POST /apiconteudos/v1/token` com `{\"senha\":\"...\"}` (sem Authorize).\n" +
            "2. Clique em **Authorize** e informe `Bearer <token>`.\n" +
            "3. Use **Try it out** nos demais endpoints.\n\n" +
            "Os mesmos recursos existem em `/api/v1` **sem JWT** (documento separado na UI)."
    });
    c.SwaggerDoc(SwaggerDocPaths.ApiInterna, new OpenApiInfo
    {
        Title = "MDW Conteúdos — API Interna",
        Version = "v1",
        Description =
            "Base **/api/v1** sem autenticação. Uso interno (laudos HTML, integrações locais). " +
            "Parceiros externos devem usar **/apiconteudos/v1** com JWT."
    });
    c.SwaggerDoc(SwaggerDocPaths.Web, new OpenApiInfo
    {
        Title = "MDW Conteúdos — Web Admin",
        Version = "v1",
        Description =
            "API do sistema web migrado (cadastros, scripts, usuários). " +
            "Requer JWT de login web (`POST /api/web/auth/login`), não o JWT de parceiros."
    });

    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        var path = apiDesc.RelativePath ?? "";
        return docName switch
        {
            SwaggerDocPaths.Parceiros => path.StartsWith("apiconteudos/v1", StringComparison.OrdinalIgnoreCase),
            SwaggerDocPaths.ApiInterna =>
                path.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("api/conversions", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("api/voice", StringComparison.OrdinalIgnoreCase),
            SwaggerDocPaths.Web => path.StartsWith("api/web", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    });

    c.AddSecurityDefinition("PartnerJwt", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description =
            "JWT de parceiro. Obtenha em POST /apiconteudos/v1/token. " +
            "Informe: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.OperationFilter<PartnerJwtOperationFilter>();
    c.OperationFilter<ApiPublicaExamplesOperationFilter>();
});

var jwtSecret = builder.Configuration["WebAuth:JwtSecret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    jwtSecret = DefaultWebJwtSecret;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["WebAuth:Issuer"] ?? "MdwConteudos",
            ValidAudience = builder.Configuration["WebAuth:Audience"] ?? "MdwConteudos.Web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(_ => true)
    .AllowCredentials()));

var app = builder.Build();
app.UseCors();
app.UseRequestTimeouts();
var repoRoot = FindRepoRoot(builder.Environment.ContentRootPath) ?? Directory.GetCurrentDirectory();
var migracaoStaticDir = Path.Combine(repoRoot, "mdw-migracao", "static");
if (Directory.Exists(migracaoStaticDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(migracaoStaticDir),
        RequestPath = "/static"
    });
}
app.UseMiddleware<ApiPartnerJwtMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "MDW Conteúdos — Documentação API";
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint($"/swagger/{SwaggerDocPaths.Parceiros}/swagger.json", "API Parceiros (JWT)");
    c.SwaggerEndpoint($"/swagger/{SwaggerDocPaths.ApiInterna}/swagger.json", "API Interna (/api/v1 + conversions)");
    c.SwaggerEndpoint($"/swagger/{SwaggerDocPaths.Web}/swagger.json", "Web Admin (/api/web)");
    c.DisplayRequestDuration();
    c.EnableTryItOutByDefault();
});
app.MapGet("/apiconteudos/docs", () => Results.Redirect("/swagger/index.html?urls.primaryName=API%20Parceiros%20(JWT)"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var v in values)
        if (!string.IsNullOrWhiteSpace(v)) return v;
    return null;
}

static string? FindRepoRoot(string startDir)
{
    var dir = startDir;
    for (var i = 0; i < 8; i++)
    {
        if (File.Exists(Path.Combine(dir, "app.py")) || File.Exists(Path.Combine(dir, ".env")))
            return dir;
        var parent = Directory.GetParent(dir);
        if (parent == null) break;
        dir = parent.FullName;
    }
    return null;
}

static string GetMigrationRoot(string contentRoot)
{
    var root = FindRepoRoot(contentRoot);
    return root != null && Directory.Exists(Path.Combine(root, "mdw-migracao"))
        ? Path.Combine(root, "mdw-migracao")
        : Path.GetFullPath(Path.Combine(contentRoot, "..", ".."));
}
