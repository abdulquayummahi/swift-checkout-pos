namespace SwiftCheckoutPOS.UI.Models;

/// <summary>BP-11: simple list paging. Page size 100; the list keeps all history, only the screen is split.</summary>
public sealed record PageInfo(int Page, int PageSize, int TotalItems, string Action, IReadOnlyDictionary<string, string?> Route)
{
    public const int DefaultSize = 100;
    public int PageCount => Math.Max(1, (TotalItems + PageSize - 1) / PageSize);
    public int First => TotalItems == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int Last => Math.Min(Page * PageSize, TotalItems);

    public static (IReadOnlyList<T> Items, PageInfo Info) Of<T>(IEnumerable<T> all, int? page, string action, IReadOnlyDictionary<string, string?> route)
    {
        var list = all as IReadOnlyList<T> ?? all.ToList();
        var count = Math.Max(1, (list.Count + DefaultSize - 1) / DefaultSize);
        var p = Math.Clamp(page ?? 1, 1, count);
        return (list.Skip((p - 1) * DefaultSize).Take(DefaultSize).ToList(), new PageInfo(p, DefaultSize, list.Count, action, route));
    }
}
