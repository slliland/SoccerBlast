namespace SoccerBlast.Api.Services.Search;

public static class EnumerableExt
{
    public static List<T> DistinctByKey<T>(
        this IEnumerable<T> items,
        Func<T, string?> keySelector,
        StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;

        var seen = new HashSet<string>(comparer);
        var list = new List<T>();

        foreach (var item in items)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (seen.Add(key.Trim())) list.Add(item);
        }

        return list;
    }
}
