using ConversorHtml.Application.Services;

namespace ConversorHtml.Tests;

public class LaudosUxHtmlValidatorTests
{
    private readonly LaudosUxHtmlValidator _validator = new();

    private const string ValidHtml = """
        <style>
            .title { font-weight: bold; }
            .campoMedida { width: 63px; }
        </style>
        <div id="containerHtml">
            <div class="d-flex">
                <div id="CollapseColunaEsquerda" ats="imprimir" percent="50" indexImpressao="1">
                    <div class="title">DADOS</div>
                    <div>
                        <div class="descricaoMedida">
                            <span for="VR_ALTURA">Altura</span>
                        </div>
                        <div>
                            <input id="VR_ALTURA" class="campoMedida" type="number" step="any">
                            <span for="VR_ALTURA" class="referencia" id="VR_ALTURA_NORMALIDADE">100 - 220</span>
                            <span for="VR_ALTURA" id="VR_ALTURA_COMENTARIONORMALIDADE"></span>
                        </div>
                    </div>
                </div>
                <div id="CollapseColunaDireita" ats="imprimir" percent="50" indexImpressao="2">
                    <div class="title">MEDIDAS</div>
                    <div>
                        <div class="descricaoMedida">
                            <span for="VR_PESO">Peso</span>
                        </div>
                        <div>
                            <input id="VR_PESO" class="campoMedida" type="number" step="any">
                            <span for="VR_PESO" class="referencia" id="VR_PESO_NORMALIDADE">30 - 200</span>
                            <span for="VR_PESO" id="VR_PESO_COMENTARIONORMALIDADE"></span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        <script>
            function iniciarFuncoes() {
                try { } catch (e) { console.error(e); }
            }
            iniciarFuncoes();
        </script>
        """;

    [Fact]
    public void ValidHtml_ShouldPass()
    {
        var result = _validator.Validate(ValidHtml);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingContainerHtml_ShouldFail()
    {
        var html = ValidHtml.Replace("id=\"containerHtml\"", "id=\"outro\"");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("containerHtml"));
    }

    [Fact]
    public void InputWithoutVrPrefix_ShouldFail()
    {
        var html = ValidHtml.Replace("id=\"VR_ALTURA\"", "id=\"ALTURA\"");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("VR_"));
    }

    [Fact]
    public void PrintBlockWithoutCollapse_ShouldFail()
    {
        var html = ValidHtml.Replace("id=\"CollapseColunaEsquerda\"", "id=\"ColunaEsquerda\"");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Collapse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingCampoMedida_ShouldFail()
    {
        var html = ValidHtml.Replace("class=\"campoMedida\"", "class=\"outro\"");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("campoMedida"));
    }

    [Fact]
    public void ForbiddenDomContentLoaded_ShouldFail()
    {
        var html = ValidHtml.Replace(
            "iniciarFuncoes();",
            "document.addEventListener('DOMContentLoaded', function(){});\niniciarFuncoes();");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("DOMContentLoaded"));
    }

    [Fact]
    public void InlineCss_ShouldFail()
    {
        var html = ValidHtml.Replace("<div class=\"d-flex\">", "<div class=\"d-flex\" style=\"gap:10px\">");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("inline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TableLayout_ShouldFail()
    {
        var html = ValidHtml.Replace("<div class=\"d-flex\">", "<table><tr><td></td></tr></table><div class=\"d-flex\">");
        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("<table>"));
    }

    [Fact]
    public void CssClassRecommendations_AreNotErrors()
    {
        var html = ValidHtml.Replace(".title { font-weight: bold; }", ".container { width: 100%; } .title { font-weight: bold; }");
        var result = _validator.Validate(html);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.DoesNotContain(result.Errors, e => e.Contains(".container"));
    }

    [Fact]
    public void MissingNormalidade_ShouldFailWithPersonalizedMessage()
    {
        var html = System.Text.RegularExpressions.Regex.Replace(
            ValidHtml,
            @"<span for=""VR_ALTURA"" class=""referencia"" id=""VR_ALTURA_NORMALIDADE"">[\s\S]*?</span>\s*",
            "");

        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("Padrões de Desenvolvimento", StringComparison.OrdinalIgnoreCase)
            && e.Contains("VR_ALTURA_NORMALIDADE")
            && e.Contains("normalidade", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingComentarioNormalidade_ShouldFailWithPersonalizedMessage()
    {
        var html = System.Text.RegularExpressions.Regex.Replace(
            ValidHtml,
            @"<span for=""VR_ALTURA"" id=""VR_ALTURA_COMENTARIONORMALIDADE""></span>",
            "");

        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("Padrões de Desenvolvimento", StringComparison.OrdinalIgnoreCase)
            && e.Contains("VR_ALTURA_COMENTARIONORMALIDADE")
            && e.Contains("comentário", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WrongNormalidadeId_ShouldFailWithExpectedFormat()
    {
        var html = ValidHtml.Replace(
            "id=\"VR_ALTURA_NORMALIDADE\"",
            "id=\"NORMALIDADE_ALTURA\"");

        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("VR_ALTURA_NORMALIDADE")
            && (e.Contains("NORMALIDADE_ALTURA") || e.Contains("sufixo _NORMALIDADE")));
    }

    [Fact]
    public void NormalidadeWithWrongFor_ShouldFail()
    {
        var html = ValidHtml.Replace(
            "<span for=\"VR_ALTURA\" class=\"referencia\" id=\"VR_ALTURA_NORMALIDADE\">",
            "<span for=\"VR_PESO\" class=\"referencia\" id=\"VR_ALTURA_NORMALIDADE\">");

        var result = _validator.Validate(html);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("VR_ALTURA_NORMALIDADE")
            && e.Contains("for=\"VR_ALTURA\""));
    }
}
