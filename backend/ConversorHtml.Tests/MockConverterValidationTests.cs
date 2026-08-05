using ConversorHtml.Application.Services;
using Xunit;

namespace ConversorHtml.Tests;

public class MockConverterValidationTests
{
    [Fact]
    public async Task MockGeneratedHtml_ShouldBeValid()
    {
        var converter = new MockImageToHtmlConverter();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var html = await converter.ConvertAsync(stream, "teste.png");
        var result = new LaudosUxHtmlValidator().Validate(html);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }
}
