using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Enums;
using Xunit;

namespace ConversorHtml.Tests;

public class MockConverterValidationTests
{
    [Fact]
    public async Task MockGeneratedHtml_ShouldBeValid()
    {
        var converter = new MockImageToHtmlConverter();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var html = await converter.ConvertAsync(stream, "teste.png", ConversionOutputFormat.Html);
        var result = new LaudosUxHtmlValidator().Validate(html);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public async Task MockGeneratedModoTexto_ShouldBeValid()
    {
        var converter = new MockImageToHtmlConverter();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var text = await converter.ConvertAsync(stream, "teste.png", ConversionOutputFormat.ModoTexto);
        var result = new LaudosUxModoTextoValidator().Validate(text);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }
}
