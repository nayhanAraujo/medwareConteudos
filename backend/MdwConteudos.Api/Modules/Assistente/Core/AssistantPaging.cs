namespace MdwConteudos.Api.Modules.Assistente.Core;

public sealed record AssistantPageRequest(int Page = 1, int PageSize = 25, string? Search = null)
{
    public const int MaxPageSize = 200;
    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public int Offset => checked((NormalizedPage - 1) * NormalizedPageSize);
    public string? NormalizedSearch => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record AssistantPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public static AssistantPage<T> Create(IEnumerable<T> items, int total, AssistantPageRequest request) =>
        new(items.ToArray(), Math.Max(0, total), request.NormalizedPage, request.NormalizedPageSize);
}
