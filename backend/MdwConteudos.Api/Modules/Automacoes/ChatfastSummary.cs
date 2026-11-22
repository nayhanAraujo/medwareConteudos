using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Automacoes;

// No arbitrary labels, URLs, names or narrative can enter the public contract.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastStatistic(string Code, decimal? Value, decimal? PreviousValue = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastDailyVolume(DateOnly Date, int Events);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastTrend(DateOnly Start, DateOnly End, decimal? DeviationsPer100);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastSummary(IReadOnlyList<ChatfastStatistic> Statistics, IReadOnlyList<ChatfastDailyVolume> DailyVolume,
    IReadOnlyList<ChatfastTrend> Trend, IReadOnlyList<string> MethodCodes, IReadOnlyList<string> LimitationCodes);

public static class ChatfastSummaryContract
{
    public static readonly string[] Counts = ["analyzedConversations", "handledConversations", "newConversations", "outOfPatternConversations", "longWaitOver60Snapshot", "criticalSelections", "deduplicatedDeviations", "deviationEvents", "events", "recurrenceEvents", "noReplyNewConversations", "firstReplyMeasurements", "audioMessages", "businessDays"];
    public static readonly string[] DecimalCodes = ["deviationsPer100", "medianFirstReplyMinutes", "meanFirstReplyMinutes"];
    public static readonly string[] Methods = ["businessHours08001800", "uniqueTechnicianConversation", "newClientConversationFirstReply", "globalConversationDeduplication", "criticalSelectionsNotUniqueCases", "longWaitSnapshot", "dailyOutOfPatternWindows", "botAuthoritativeState"];
    public static readonly string[] Limitations = ["unequalBusinessDays", "partialCollectionLastDay", "missingDeploymentSummary", "incompleteCriticalEventExtraction", "recurrenceCoverageChanged", "limitedFirstReplySample", "audioTranscriptionPending", "deviationsOnlyMonitoringWindows", "snapshotNotLive", "sourceFileNotRawAudit", "noTreatmentEvidence", "trackerEmptyNotNoCriticalCases"];
    public static void Validate(ChatfastSummary s, ChatfastReportRequest report)
    {
        void Require(bool valid) { if (!valid) throw new ChatfastException(400, "summary inválido: use apenas indicadores globais e códigos documentados."); }
        Require(s.Statistics is { Count: <= 30 } && s.DailyVolume is { Count: <= 8 } && s.Trend is { Count: <= 12 } && s.MethodCodes is { Count: <= 20 } && s.LimitationCodes is { Count: <= 20 });
        Require(s.Statistics.All(x => x is not null) && s.DailyVolume.All(x => x is not null) && s.Trend.All(x => x is not null));
        Require(s.Statistics.Select(x => x.Code).Distinct().Count() == s.Statistics.Count);
        foreach (var stat in s.Statistics)
        {
            Require(Counts.Contains(stat.Code) || DecimalCodes.Contains(stat.Code));
            foreach (var value in new[] { stat.Value, stat.PreviousValue })
                Require(value is null || value >= 0 && value <= 1_000_000_000 && (DecimalCodes.Contains(stat.Code) || value == decimal.Truncate(value.Value)));
        }
        Require(s.MethodCodes.All(Methods.Contains) && s.LimitationCodes.All(Limitations.Contains));
        Require(s.MethodCodes.Distinct().Count() == s.MethodCodes.Count && s.LimitationCodes.Distinct().Count() == s.LimitationCodes.Count);
        Require(s.DailyVolume.Select(x => x.Date).Distinct().Count() == s.DailyVolume.Count);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(report.PeriodStart, zone).DateTime);
        var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(report.PeriodEnd.AddTicks(-1), zone).DateTime);
        Require(s.DailyVolume.All(x => x.Events >= 0 && x.Date >= start && x.Date <= end));
        Require(s.Trend.All(x => x.Start <= x.End && x.End <= end && x.End.DayNumber - x.Start.DayNumber <= 7 && (x.DeviationsPer100 is null || x.DeviationsPer100 >= 0 && x.DeviationsPer100 <= 1_000_000_000)));
    }
}
