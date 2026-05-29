using System.Globalization;

namespace MdwConteudos.Api.Infrastructure;

public static class JsonHelper
{
    public static object? ToJsonSafe(object? value)
    {
        if (value is null or DBNull) return null;
        if (value is decimal d) return (double)d;
        if (value is DateTime dt) return dt.ToString("o", CultureInfo.InvariantCulture);
        if (value is DateOnly dOnly) return dOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return value;
    }

    public static Dictionary<string, object?> RowToDict(IEnumerable<KeyValuePair<string, object?>> row)
    {
        return row.ToDictionary(k => k.Key, k => ToJsonSafe(k.Value), StringComparer.OrdinalIgnoreCase);
    }
}
