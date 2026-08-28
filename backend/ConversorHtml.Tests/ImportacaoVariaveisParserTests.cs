using MdwConteudos.Api.Modules.Web;

namespace ConversorHtml.Tests;

public sealed class ImportacaoVariaveisParserTests
{
    [Fact]
    public void Json_NormalizaVariavelFormulaENormalidade()
    {
        const string json = """{"variaveis":[{"sigla":"VR_IMC","nome":"IMC","unidade":"kg/m2"}],"formulas":[{"sigla":"VR_IMC","formula":"peso / altura"}],"normalidades":[{"sigla":"VR_IMC","sexo":"A","valormin":18.5,"valormax":25,"idade_min":18,"idade_max":120,"codreferencia":7}]}""";
        var result = JsonVariaveisParser.Parse(json, "dados.json");
        Assert.Single(result.Variaveis); Assert.Single(result.Formulas); Assert.Single(result.Normalidades);
        Assert.Equal("VR_IMC", result.Variaveis[0].Codigo); Assert.Equal(7, result.Normalidades[0].CodReferencia);
    }

    [Fact]
    public void Json_InvalidoGeraErroClaro()
    {
        var error = Assert.Throws<InvalidOperationException>(() => JsonVariaveisParser.Parse("{", "dados.json"));
        Assert.Contains("JSON inválido", error.Message);
    }

    [Fact]
    public void JsonStudio_ExtraiCamposFormulaEReferenciaNormalidade()
    {
        const string json = """{"camposScript":[{"tipo":"etiqueta","nome":"DADOS"},{"tipo":"numero","nome":"IMC","descricao":"Índice de massa corporal","medida":"kg/m²","casasDecimais":"2","funcao":"<<VR_PESO>> / 2","normalidades":[{"sexo":"M","valorMin":"0","valorMax":"10","cor":"verde"}],"referenciaNormalidade":[{"sexo":"M","valorMin":"18,5","valorMax":"24,9"}]}]}""";
        var result = JsonVariaveisParser.Parse(json, "modelo Studio.json");
        Assert.Equal("json-studio", result.Formato); Assert.Single(result.Variaveis); Assert.Single(result.Formulas); Assert.Single(result.Normalidades);
        Assert.Equal("VR_IMC", result.Variaveis[0].Codigo); Assert.Equal(18.5m, result.Normalidades[0].ValorMin); Assert.Single(result.Avisos);
    }

    [Fact]
    public void JsonNormalidadesMedware_ExtraiFaixasMetadadosComentariosEAliases()
    {
        const string json = """
            {
              "VR_AO_SEIOS_VALSALVA": {
                "F": { "normal": { "min": 2.7, "max": 3.3 }, "leve": { "min": 3.4, "max": 3.6 } },
                "U": { "grave": { "min": 4.5, "max": 9.9 } },
                "COMENTARIOTEXTO": { "comentario": "Interpretar conforme superfície corporal." },
                "_meta": { "Ano": 2015, "Fonte": "Recommendations for Cardiac Chamber Quantification by Echocardiography in Adults", "Pagina": 12 },
                "VARIAVEISALTERNATIVAS": ["VR_AO"]
              }
            }
            """;

        var result = JsonVariaveisParser.Parse(json, "normalidades.json");

        Assert.Equal("json-normalidades-medware", result.Formato);
        var variable = Assert.Single(result.Variaveis);
        Assert.Equal("VR_AO_SEIOS_VALSALVA", variable.Codigo);
        Assert.Contains("VR_AO", variable.AlternativasArquivo!);
        Assert.Equal(3, result.Normalidades.Count);
        Assert.Contains(result.Normalidades, item => item.Sexo == "A" && item.Classificacao == "grave");
        Assert.All(result.Normalidades, item =>
        {
            Assert.Equal(2015, item.AnoReferencia);
            Assert.Equal(12, item.Pagina);
            Assert.Equal("Interpretar conforme superfície corporal.", item.Comentario);
        });
    }

    [Fact]
    public void Roslyn_EncontraVariavelEFormulaSemExecutarCodigo()
    {
        const string source = """
            namespace Exemplo;
            public class Calculos {
              private string codigo = "VR_IMC";
              public decimal CalcularImc(decimal peso, decimal altura) { return peso / (altura * altura); }
            }
            """;
        var result = CSharpVariaveisParser.Parse(source, "calculos.cs");
        Assert.Contains(result.Variaveis, x => x.Codigo == "VR_IMC");
        Assert.Contains(result.Formulas, x => x.Variavel == "VR_IMC" && x.Expressao.Contains("peso"));
    }

    [Fact]
    public void Roslyn_ArquivoSemVariaveisRetornaListaVazia()
    {
        var result = CSharpVariaveisParser.Parse("public class SemVariavel { }", "vazio.cs");
        Assert.Empty(result.Variaveis);
    }
}
