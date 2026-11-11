using System.Globalization;
using System.Text;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>
/// Spreadsheet export (CSV, UTF-8 with a byte-order mark so Excel shows Bangla and the taka sign correctly).
/// Text that a spreadsheet would run as a formula (= + - @ at the start) is prefixed with an apostrophe.
/// </summary>
public static class Csv
{
    public static byte[] Write(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", headers.Select(Cell)));
        foreach (var row in rows) text.AppendLine(string.Join(",", row.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    public static string Cell(object? value)
    {
        var text = value switch
        {
            null => "",
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            DateTime t => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
