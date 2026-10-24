using System.Globalization;
using System.Text.RegularExpressions;

namespace ETL.Data.Services;

/// <summary>
/// Divide líneas CSV respetando campos entre comillas dobles (comas dentro del comentario).
/// </summary>
public static class CsvLineParser
{
    private static readonly Regex FieldSplitRegex = new(
        ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)",
        RegexOptions.Compiled);

    public static string[] SplitFields(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return Array.Empty<string>();

        return FieldSplitRegex
            .Split(line)
            .Select(static f => Unquote(f.Trim()))
            .ToArray();
    }

    public static string Unquote(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\"\"", "\"");

        return value.Trim('"', ' ');
    }

    public static bool TryParseDate(string raw, out DateTime fecha)
    {
        raw = raw?.Trim() ?? string.Empty;
        string[] formats =
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy",
            "M/d/yyyy",
            "MM/dd/yyyy"
        };

        return DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
               || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }

    /// <summary>
    /// Unifica identificadores (ej. C019 → 19, P003 → 3, 8537 → 8537).
    /// </summary>
    public static string? NormalizarId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();

        if (raw.Length > 1 && (raw[0] == 'C' || raw[0] == 'c' || raw[0] == 'P' || raw[0] == 'p'))
            raw = raw[1..];

        raw = raw.Trim();

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numero))
            return numero.ToString(CultureInfo.InvariantCulture);

        return raw;
    }
}
