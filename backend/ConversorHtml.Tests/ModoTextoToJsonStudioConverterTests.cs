using ConversorHtml.Application.Services;

namespace ConversorHtml.Tests;

public class ModoTextoToJsonStudioConverterTests
{
    private const string Sample = """
[DADOS GERAIS]
Altura (ALTURA): 0.0  cm (F:  a ) (M:  a )
Peso (PESO): 0.0  kg (F:  a ) (M:  a )
Sup. Corp (SUPCOR): 0.0  m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (0.007184 * Math.pow(<<VR_PESO>>, 0.425) * Math.pow(<<VR_ALTURA>>, 0.725)).toFixed(2) } else { '' }
IMC (IMC): 0.0  kg/m² (F:  a ) (M:  a )  Código: if(<<VR_PESO>> > 0 && <<VR_ALTURA>> > 0) { (<<VR_PESO>> / Math.pow(<<VR_ALTURA>> / 100, 2)).toFixed(1) } else { '' }
Ritmo (RITMO) = Ritmo Sinusal, Fibrilação Atrial, Flutter Atrial [Lista]

[AORTA]
Anel (AO): 0.0  mm (M: 19 a 23.4) (F: 17.4 a 21.6)  Comentário: (F: {17.4, 21.6, , verde }),(M: {19, 23.4, , verde })
""";

    [Fact]
    public void Convert_ShouldProduceCamposScriptRoot()
    {
        var json = ModoTextoToJsonStudioConverter.Convert(Sample);
        Assert.Contains("\"camposScript\"", json);
        Assert.Contains("\"tipo\": \"etiqueta\"", json);
        Assert.Contains("\"tipo\": \"numero\"", json);
        Assert.Contains("\"tipo\": \"combo\"", json);
        Assert.Contains("LAUDODESCRITIVO", json);
        Assert.Contains("<<VR_PESO>>", json);
    }

    [Fact]
    public void Convert_ShouldMapCodigoToFuncao()
    {
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(Sample);
        var imc = campos.First(n => n?["nome"]?.GetValue<string>() == "IMC");
        Assert.Equal("numero", imc!["tipo"]!.GetValue<string>());
        Assert.Contains("<<VR_ALTURA>>", imc["funcao"]!.GetValue<string>());
    }

    [Fact]
    public void Convert_ShouldMapListaToCombo()
    {
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(Sample);
        var ritmo = campos.First(n => n?["nome"]?.GetValue<string>() == "RITMO");
        Assert.Equal("combo", ritmo!["tipo"]!.GetValue<string>());
        Assert.True(ritmo["opcoes"]!.AsArray().Count >= 3);
    }
}
