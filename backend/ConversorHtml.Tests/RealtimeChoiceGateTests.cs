using MdwConteudos.Api.Modules.RealtimeVoice;

namespace ConversorHtml.Tests;

public class RealtimeChoiceGateTests
{
    [Fact]
    public void ParallelMutationWaitsForSearchAndCannotGuessAnAmbiguousCandidate()
    {
        var gate = new RealtimeChoiceGate();
        var search = gate.Queue("search", "buscar_medidas");
        var include = gate.Queue("include", "incluir_medidas");
        Assert.False(gate.CanExecute(include, "incluir_medidas"));
        Assert.False(gate.RequiresDecision(include)); // Search is still running; no human question yet.
        Assert.True(gate.Offer(search, "signed-question"));
        gate.EndSearch("search");
        Assert.True(gate.HasPending);
        Assert.True(gate.RequiresDecision(include));
        Assert.False(gate.CanExecute(include, "incluir_medidas"));
        Assert.False(gate.CanExecute(gate.Queue("another-search", "buscar_medidas"), "buscar_medidas"));
        gate.EndSearch("another-search");
        Assert.True(gate.CanExecute(include, "consultar_rascunho"));
        Assert.False(gate.Complete("different-question"));
        Assert.True(gate.HasPending);
        Assert.True(gate.Complete("signed-question"));
        Assert.False(gate.CanExecute(include, "incluir_medidas")); // A queued guess stays obsolete after cancellation/selection.
        Assert.True(gate.CanExecute(gate.Queue("new-intention", "incluir_medidas"), "incluir_medidas"));
    }

    [Fact]
    public void AUniqueSearchResultDoesNotLeaveTheAgentWaitingForANonexistentModal()
    {
        var gate = new RealtimeChoiceGate();
        gate.Queue("search", "buscar_medidas");
        var include = gate.Queue("include", "incluir_medidas");
        Assert.False(gate.CanExecute(include, "incluir_medidas"));
        gate.EndSearch("search");
        Assert.False(gate.HasPending);
        Assert.False(gate.RequiresDecision(include));
        Assert.True(gate.CanExecute(include, "incluir_medidas"));
    }

    [Fact]
    public void LateParallelSearchCannotReplaceAQuestionOrReopenItAfterTheUserAnswers()
    {
        var gate = new RealtimeChoiceGate();
        var first = gate.Queue("first", "buscar_medidas");
        var second = gate.Queue("second", "buscar_medidas");
        Assert.True(gate.Offer(first, "question-1"));
        Assert.False(gate.Offer(second, "question-2"));
        Assert.True(gate.Complete("question-1"));
        Assert.False(gate.Offer(second, "late-question-2"));
        gate.EndSearch("first"); gate.EndSearch("second");
        Assert.True(gate.Offer(gate.Queue("fresh-search", "buscar_medidas"), "question-3"));
    }
}
