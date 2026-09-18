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
    public void Convert_ShouldDisableGeraGraficoOnNumericFields()
    {
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(Sample);
        var numeros = campos.Where(n => n?["tipo"]?.GetValue<string>() == "numero");
        Assert.All(numeros, n => Assert.False(n!["geraGrafico"]!.GetValue<bool>()));
    }

    [Fact]
    public void Convert_ShouldMapCodigoOnlyWhenPresentInTxt()
    {
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(Sample);
        var altura = campos.First(n => n?["nome"]?.GetValue<string>() == "ALTURA");
        var peso = campos.First(n => n?["nome"]?.GetValue<string>() == "PESO");
        var sc = campos.First(n => n?["nome"]?.GetValue<string>() == "SUPCOR");

        Assert.Equal("", altura!["funcao"]!.GetValue<string>());
        Assert.Equal("", peso!["funcao"]!.GetValue<string>());
        Assert.Contains("0.007184", sc!["funcao"]!.GetValue<string>());
    }

    [Fact]
    public void Convert_ShouldMapListaToCombo()
    {
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(Sample);
        var ritmo = campos.First(n => n?["nome"]?.GetValue<string>() == "RITMO");
        Assert.Equal("combo", ritmo!["tipo"]!.GetValue<string>());
        Assert.True(ritmo["opcoes"]!.AsArray().Count >= 3);
    }

    [Fact]
    public void Convert_ShouldFillReferenciaFromComentarioWhenMissingSimpleRanges()
    {
        const string onlyComment = """
[AORTA]
Anel (AO): 0.0  mm  Comentário: (F: {25,7, 32,9, verde }),(M: {28,5, 35,9, verde })
""";
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(onlyComment);
        var ao = campos.First(n => n?["nome"]?.GetValue<string>() == "AO");
        var refs = ao!["referenciaNormalidade"]!.AsArray();
        var norms = ao["normalidades"]!.AsArray();

        Assert.True(norms.Count >= 2);
        Assert.Equal(2, refs.Count);

        var f = refs.First(r => r?["sexo"]?.GetValue<string>() == "F");
        Assert.Equal("25,70", f!["valorMin"]!.GetValue<string>());
        Assert.Equal("32,90", f["valorMax"]!.GetValue<string>());
        Assert.Equal("mm", f["unidadeMedida"]!.GetValue<string>());
        Assert.Contains("25,70 a", f["valorExtenso"]!.GetValue<string>());

        var normF = norms.First(n => n?["sexo"]?.GetValue<string>() == "F");
        Assert.Equal("25.70", normF!["valorMin"]!.GetValue<string>());
        Assert.Equal("32.90", normF["valorMax"]!.GetValue<string>());
        Assert.Equal("verde", normF["descricao"]!.GetValue<string>());
    }

    [Fact]
    public void Convert_ShouldKeepReferenciaWithCommaDecimalsFromSimpleRanges()
    {
        const string withRefs = """
[AORTA]
Anel (AO): 0.0  mm (M: 28.5 a 35.9) (F: 25.7 a 32.9)
""";
        var campos = ModoTextoToJsonStudioConverter.BuildCampos(withRefs);
        var ao = campos.First(n => n?["nome"]?.GetValue<string>() == "AO");
        var f = ao!["referenciaNormalidade"]!.AsArray().First(r => r?["sexo"]?.GetValue<string>() == "F");
        Assert.Equal("25,70", f!["valorMin"]!.GetValue<string>());
        Assert.Equal("32,90", f["valorMax"]!.GetValue<string>());
        Assert.Equal("25,70 a  32,90", f["valorExtenso"]!.GetValue<string>());
    }
}
