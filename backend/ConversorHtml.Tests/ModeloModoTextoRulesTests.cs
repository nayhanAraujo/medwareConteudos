using ConversorHtml.Application.Services;

namespace ConversorHtml.Tests;

public class ModeloModoTextoRulesTests
{
    [Fact]
    public void ExtractDependencyTokens_RecognizesBareAndVrTokens()
    {
        var tokens = ModeloModoTextoRules.ExtractDependencyTokens(
            "if(VR_PESO > 0) { PESO / Math.pow(ALTURA, 2) }",
            ["PESO", "ALTURA", "IMC"]);

        Assert.Equal(["PESO", "ALTURA"], tokens);
    }

    [Fact]
    public void ResolveDependencyOrder_PutsDependenciesBeforeCalculatedMeasure()
    {
        var graph = new Dictionary<int, IReadOnlyList<int>>
        {
            [3] = [1, 2],
            [1] = [],
            [2] = []
        };

        Assert.Equal([1, 2, 3], ModeloModoTextoRules.ResolveDependencyOrder([3], graph));
    }

    [Fact]
    public void ResolveDependencyOrder_RejectsCycles()
    {
        var graph = new Dictionary<int, IReadOnlyList<int>> { [1] = [2], [2] = [1] };
        Assert.Throws<InvalidOperationException>(() => ModeloModoTextoRules.ResolveDependencyOrder([1], graph));
    }

    [Fact]
    public void ComposeTwoColumns_PreservesConfiguredSecondColumnBreak()
    {
        string[] left = ["[ESQUERDA]", "Campo (A): 0.0 cm", ""];
        string[] right = ["[DIREITA]", "Campo (B): 0.0 cm", "Outro (C): 0.0 cm", "Mais (D): 0.0 cm", ""];

        var text = ModeloModoTextoRules.ComposeTwoColumns(left, right);
        var lines = text.Split(Environment.NewLine).ToList();

        Assert.Equal("[DIREITA]", ModeloModoTextoRules.InferSecondColumnHeader(lines));
    }
}
