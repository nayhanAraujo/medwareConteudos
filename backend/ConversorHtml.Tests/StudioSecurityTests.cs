using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Models;
using MdwConteudos.Api.Controllers;
using MdwConteudos.Api.Modules.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversorHtml.Tests;

public class StudioSecurityTests
{
    [Theory]
    [InlineData(typeof(ConversionsController))]
    [InlineData(typeof(VoiceSessionsController))]
    public void ControllersRequireWebJwt(Type controller)
    {
        Assert.Equal("Bearer", controller.GetCustomAttribute<AuthorizeAttribute>()?.AuthenticationSchemes);
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var method in controller.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)))
        {
            if (method.Name == nameof(ConversionsController.Health)) continue;
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            var permissions = controller.GetCustomAttributes<RequirePermissionAttribute>()
                .Concat(method.GetCustomAttributes<RequirePermissionAttribute>()).ToArray();
            Assert.Contains(permissions, p => p.Arguments!.SequenceEqual(new object[] { "studio", "visualizar" }));
            Assert.Contains(permissions, p => p.Arguments!.SequenceEqual(new object[] { "studio", controller == typeof(ConversionsController) ? "converter" : "voz" }));
        }
    }

    [Fact]
    public void AlternativeRegistrationRequiresVariableCreation()
    {
        var method = typeof(ConversionsController).GetMethod(nameof(ConversionsController.RegisterAlternativa))!;
        Assert.Contains(method.GetCustomAttributes<RequirePermissionAttribute>(),
            p => p.Arguments!.SequenceEqual(new object[] { "variaveis", "criar" }));
    }

    [Theory]
    [InlineData(typeof(ConversionsController), "Convert")]
    [InlineData(typeof(ConversionsController), "Analyze")]
    [InlineData(typeof(ConversionsController), "GenerateModoTextoFromAnalysis")]
    [InlineData(typeof(ConversionsController), "GenerateJsonFromAnalysis")]
    [InlineData(typeof(VoiceSessionsController), "CreateSession")]
    [InlineData(typeof(VoiceSessionsController), "ApplyUtterance")]
    [InlineData(typeof(VoiceSessionsController), "Generate")]
    [InlineData(typeof(VoiceSessionsController), "Transcribe")]
    public void ProcessingIsRateLimited(Type controller, string action)
        => Assert.Equal("studio", controller.GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

    [Fact]
    public void HealthIsAnonymousAndMinimal()
    {
        var controller = new ConversionsController(null!, null!, null!, null!, null!, NullLogger<ConversionsController>.Instance);
        var method = typeof(ConversionsController).GetMethod(nameof(ConversionsController.Health))!;
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Empty(method.GetCustomAttributes<RequirePermissionAttribute>());
        Assert.Empty(typeof(ConversionsController).GetCustomAttributes<RequirePermissionAttribute>());
        var result = Assert.IsType<OkObjectResult>(controller.Health());
        Assert.Equal("{\"status\":\"healthy\"}", JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("")]
    public async Task OtherUsersCannotReadMutateOrExportSession(string userId)
    {
        var store = new InMemoryVoiceSessionStore();
        var session = store.Create(new VoiceSession { Id = Guid.NewGuid(), OwnerUserId = "1", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        var controller = Controller(store, userId);
        Assert.IsType<NotFoundObjectResult>(controller.GetSession(session.Id).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.ApplyUtterance(session.Id, new VoiceUtteranceRequestDto(), default)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Generate(session.Id, new VoiceGenerateRequestDto(), default)).Result);
        Assert.Empty(session.TranscriptHistory);
    }

    [Fact]
    public async Task SessionOwnerComesFromIdentityAndCanReadSession()
    {
        var store = new InMemoryVoiceSessionStore();
        var controller = Controller(store, "42");
        var created = Assert.IsType<OkObjectResult>((await controller.CreateSession("FromScratch", null, default)).Result);
        var dto = Assert.IsType<VoiceSessionResponseDto>(created.Value);
        Assert.Equal("42", store.Get(dto.Id)!.OwnerUserId);
        Assert.IsType<OkObjectResult>(controller.GetSession(dto.Id).Result);
    }

    [Fact]
    public async Task MissingIdentityCannotCreateSession()
        => Assert.IsType<UnauthorizedResult>((await Controller(new InMemoryVoiceSessionStore(), "").CreateSession("FromScratch", null, default)).Result);

    private static VoiceSessionsController Controller(InMemoryVoiceSessionStore store, string userId)
    {
        var controller = new VoiceSessionsController(store, null!, null!, null!, null!,
            new ConfigurationBuilder().Build(), NullLogger<VoiceSessionsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    string.IsNullOrEmpty(userId) ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Bearer"))
            }
        };
        return controller;
    }
}
