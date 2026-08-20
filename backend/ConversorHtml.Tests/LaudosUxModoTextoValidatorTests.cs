using ConversorHtml.Application.Services;
using Xunit;

namespace ConversorHtml.Tests;

public class LaudosUxModoTextoValidatorTests
{
    private readonly LaudosUxModoTextoValidator _validator = new();

    private const string ValidSample = """
[DADOS GERAIS]
Altura (ALTURA): 0.0  cm (F:  a ) (M:  a )
Peso (PESO): 0.0  kg (F:  a ) (M:  a )
Sup. Corp (SUPCOR): 0.0  m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (0.007184 * Math.pow(<<VR_PESO>>, 0.425) * Math.pow(<<VR_ALTURA>>, 0.725)).toFixed(2) } else { '' }
IMC (IMC): 0.0  kg/m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (<<VR_PESO>> / Math.pow(<<VR_ALTURA>> / 100, 2)).toFixed(1) } else { '' }
Ritmo (RITMO) = Ritmo Sinusal, Fibrilação Atrial, Flutter Atrial [Lista]

[AORTA]
Anel (AO): 0.0  mm (M: 19 a 23.4) (F: 17.4 a 21.6)  Comentário: (F: {17,4, 21,6,  verde }),(M: {19, 23,4,  verde })
""";

    [Fact]
    public void ValidSample_ShouldPass()
    {
        var result = _validator.Validate(ValidSample);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void EmptyText_ShouldFail()
    {
        var result = _validator.Validate("");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("vazio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CodigoWithoutVrReference_ShouldFail()
    {
        var text = """
[DADOS]
Altura (ALTURA): 0.0  cm  Código: return 1;
""";
        var result = _validator.Validate(text);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("<<VR_", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateVariable_ShouldFail()
    {
        var text = """
[DADOS]
Altura (ALTURA): 0.0  cm
Peso (ALTURA): 0.0  kg
""";
        var result = _validator.Validate(text);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HashLine_ShouldWarnOnly()
    {
        var text = """
[DADOS]
HASH linha ignorada
Altura (ALTURA): 0.0  cm
""";
        var result = _validator.Validate(text);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("HASH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingSection_ShouldWarn()
    {
        var text = "Altura (ALTURA): 0.0  cm";
        var result = _validator.Validate(text);
        Assert.Contains(result.Warnings, w => w.Contains("seção", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VrPrefixInVariableName_ShouldWarn()
    {
        var text = """
[DADOS]
Altura (VR_ALTURA): 0.0  cm
""";
        var result = _validator.Validate(text);
        Assert.Contains(result.Warnings, w => w.Contains("VR_", StringComparison.Ordinal));
    }
}
