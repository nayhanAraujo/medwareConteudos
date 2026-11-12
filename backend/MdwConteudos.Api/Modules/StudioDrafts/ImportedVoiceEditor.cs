using System.Text.Json;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Models;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public static class ImportedVoiceEditor
{
    public static StudioDocument Document(VoiceSession session) => JsonSerializer.Deserialize<StudioDocument>(session.ImportedDocumentJson!, StudioDocumentEditor.Json)!;
    public static VoiceApplyResult Apply(VoiceSession session, VoiceStructurePlan plan, long revision, IReadOnlyList<VariavelOpcao> catalog)
    {
        lock (session.EditGate)
        {
            if (revision != session.ImportedRevision) throw new StudioException(409, "O modelo foi alterado enquanto a fala era processada. Tente novamente.");
            if (plan.Commands is null || plan.Commands.Count is < 1 or > 20) throw new StudioException(400, "Não foram reconhecidos comandos estruturais. O modelo foi preservado.");
            var original = session.ImportedDocumentJson!; var doc = Document(session);
            var undo = session.ImportedUndo.ToList(); var redo = session.ImportedRedo.ToList();
            var reversal = plan.Commands.Any(c => c.Name is "desfazer_alteracao" or "refazer_alteracao");
            if (reversal && plan.Commands.Count != 1) throw new StudioException(400, "Solicite desfazer/refazer separadamente.");
            if (reversal)
            {
                var source = plan.Commands[0].Name == "desfazer_alteracao" ? undo : redo;
                var target = plan.Commands[0].Name == "desfazer_alteracao" ? redo : undo;
                if (source.Count == 0) throw new StudioException(409, "Não há alteração para desfazer/refazer nesta sessão.");
                doc = JsonSerializer.Deserialize<StudioDocument>(source[^1], StudioDocumentEditor.Json)!;
                source.RemoveAt(source.Count - 1); target.Add(original);
                StudioJsonImport.EnsureSafeChange(Document(session), doc);
            }
            else
            {
                foreach (var call in plan.Commands)
                {
                    var (type, payload) = StudioRealtimeAdapter.Map(call.Name, call.Arguments);
                    doc = StudioDocumentEditor.Apply(doc, new(Guid.NewGuid().ToString(), revision, type, JsonSerializer.SerializeToElement(payload)), catalog);
                }
                undo.Add(original); redo.Clear();
            }
            var output = StudioJsonImport.Export(doc);
            session.ImportedDocumentJson = JsonSerializer.Serialize(doc, StudioDocumentEditor.Json);
            session.CamposScriptJson = output; session.ImportedUndo = undo; session.ImportedRedo = redo;
            // Bound session-only history; persistent drafts retain their complete history.
            if (session.ImportedUndo.Count > 50) session.ImportedUndo.RemoveAt(0);
            session.ImportedRevision++;
            return new(output, plan.Summary ?? "Estrutura atualizada.", StudioJsonImport.Problems(doc).Select(p => p.Message).ToArray(), session.ImportedRevision);
        }
    }
}
