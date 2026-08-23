using ConversorHtml.Application.Services;

namespace ConversorHtml.Tests;

public class ModoTextoCodigoFormatterTests
{
    [Fact]
    public void JsWithVrTokens_ShouldWrapAsPlaceholders()
    {
        var suffix = ModoTextoCodigoFormatter.ToCodigoSuffix(
            "if(VR_PESO > 0 && VR_ALTURA > 0) { (VR_PESO / Math.pow(VR_ALTURA / 100, 2)).toFixed(1) } else { '' }");

        Assert.Equal(
            "  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (<<VR_PESO>> / Math.pow(<<VR_ALTURA>> / 100, 2)).toFixed(1) } else { '' }",
            suffix);
    }

    [Fact]
    public void AlreadyUsingPlaceholders_ShouldKeepExpressionAfterCodigo()
    {
        var suffix = ModoTextoCodigoFormatter.ToCodigoSuffix(
            "if(<<VR_PESO>> > 0) { <<VR_PESO>> } else { '' }");

        Assert.StartsWith("  Código: ", suffix);
        Assert.Contains("<<VR_PESO>>", suffix);
        Assert.DoesNotContain("Código: Código:", suffix);
    }

    [Fact]
    public void RawDumpWithoutReferences_ShouldNotEmitCodigo()
    {
        Assert.Equal("", ModoTextoCodigoFormatter.ToCodigoSuffix("peso / altura ^ 2"));
    }

    [Fact]
    public void AlgebraicFormula_ShouldMapKnownTokens()
    {
        var suffix = ModoTextoCodigoFormatter.ToCodigoSuffix(
            "PESO / (ALTURA / 100)^2",
            ["PESO", "ALTURA", "IMC"]);

        Assert.Equal("  Código: <<VR_PESO>> / (<<VR_ALTURA>> / 100)^2", suffix);
    }

    [Fact]
    public void FormatDecimal_ShouldLimitToTwoPlaces()
    {
        Assert.Equal("19", ModoTextoCodigoFormatter.FormatDecimal(19.000000m));
        Assert.Equal("21.1", ModoTextoCodigoFormatter.FormatDecimal(21.100000m));
        Assert.Equal("21,1", ModoTextoCodigoFormatter.FormatDecimal(21.100000m, useComma: true));
    }
}
