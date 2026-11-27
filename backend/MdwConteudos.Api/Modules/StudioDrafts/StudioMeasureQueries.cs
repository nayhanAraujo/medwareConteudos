using System.Globalization;
using ConversorHtml.Application.Interfaces;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public static class StudioMeasureQueries
{
    public const string NotFoundMessage = "Não encontrei essa medida no catálogo. Tente dizer o nome completo ou consulte o banco de medidas.";

    public static object Review(StudioChoices choices, VoiceMeasureQuery query, string id, int owner, long revision,
        IReadOnlyList<VariavelOpcao> catalog, IReadOnlyList<StudioSection> sections)
    {
        var term = query.Term?.Trim();
        var compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        var candidates = string.IsNullOrEmpty(term) || term.Length > 160 ? [] : catalog.Where(item =>
            new[] { item.Nome, item.Sigla, item.Abreviacao, item.Codigo }.Any(name => !string.IsNullOrEmpty(name) &&
                compare.IndexOf(name, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).Take(30).ToArray();
        if (candidates.Length == 0) return new { code = "measure_not_found", message = NotFoundMessage };
        var destinations = sections.Select(section => new { id = section.Id, nome = section.Nome })
            .Where(section => !string.IsNullOrEmpty(section.id)).ToArray();
        if (destinations.Length == 0) return new { code = "measure_destination_required", message = "Crie uma seção no modelo antes de escolher a medida." };
        var sectionId = destinations.FirstOrDefault(section => section.id == query.SectionId)?.id;
        var token = choices.Issue(id, owner, revision, candidates.Select(candidate => candidate.CodVariavel));
        return new { code = "measure_choice_required", message = "Confirme a medida antes de incluí-la.", details = new { sessionId = id, draftId = id, revision, token, candidates, sections = destinations, sectionId } };
    }
}
