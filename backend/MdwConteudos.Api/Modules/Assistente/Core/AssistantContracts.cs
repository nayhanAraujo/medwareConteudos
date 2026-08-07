namespace MdwConteudos.Api.Modules.Assistente.Core;

public static class AssistantStatus
{
    public const short Active = -1;
    public const short Inactive = 0;

    public static bool IsValid(short status) => status is Active or Inactive;
    public static short FromBoolean(bool active) => active ? Active : Inactive;
}

public sealed record AssistantLookupItem(int Id, string Description, short Status = AssistantStatus.Active);

public sealed record AssistantOperationResult(int Id, string Message);

public sealed record AssistantImportResult(
    int CodScriptLaudo,
    int CodPagFotos,
    IReadOnlyList<int> Especialidades,
    int TotalProcedimentos);
