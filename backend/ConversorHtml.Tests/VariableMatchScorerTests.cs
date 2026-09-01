using ConversorHtml.Application.Dtos;
using MdwConteudos.Api.Services;

namespace ConversorHtml.Tests;

public class VariableMatchScorerTests
{
    [Fact]
    public void Score_ShouldMatchClinicalNameFromLabel()
    {
        var measure = new ExtractedMeasureDto
        {
            Label = "Seios de Valsalva",
            VariableName = null,
            Unit = "mm"
        };

        var variable = new VariableMatchIndex
        {
            CodVariavel = 10,
            Nome = "Anel aórtico",
            Sigla = "AO",
            Variavel = "VR_AO",
            Unidade = "mm",
            NomesClinicos = "Seios de Valsalva|Raiz aórtica"
        };

        var result = VariableMatchScorer.Score(measure, variable);

        Assert.Equal(10, result.CodVariavel);
        Assert.Equal("nomeClinico", result.Motivo);
        Assert.True(result.Score >= 88);
    }

    [Fact]
    public void Score_ShouldUseOriginalTextInQuery()
    {
        var measure = new ExtractedMeasureDto
        {
            Label = "Medida",
            OriginalText = "Átrio esquerdo",
            VariableName = null
        };

        var variable = new VariableMatchIndex
        {
            CodVariavel = 20,
            Nome = "Volume AE",
            Sigla = "VAE",
            Variavel = "VR_VAE",
            NomesClinicos = "Átrio esquerdo"
        };

        var result = VariableMatchScorer.Score(measure, variable);

        Assert.Equal("nomeClinico", result.Motivo);
        Assert.True(result.Score >= 88);
    }

    [Fact]
    public void BuildQuery_ShouldIncludeLabelVariableNameAndOriginalText()
    {
        var query = VariableMatchScorer.BuildQuery(new ExtractedMeasureDto
        {
            Label = "Altura",
            VariableName = "VR_ALTURA",
            OriginalText = "Estatura"
        });

        Assert.Contains("ALTURA", query);
        Assert.Contains("ESTATURA", query);
    }
}
