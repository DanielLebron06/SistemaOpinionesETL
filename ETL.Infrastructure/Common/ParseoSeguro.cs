using System.Globalization;

namespace ETL.Infrastructure.Common;

/// <summary>Conversión tolerante de valores de texto provenientes de las fuentes.</summary>
internal static class ParseoSeguro
{
    private static readonly string[] FormatosFecha =
    {
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy", "M/d/yyyy", "MM/dd/yyyy"
    };

    public static DateTime? Fecha(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        valor = valor.Trim();
        if (DateTime.TryParseExact(valor, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return fecha;

        return DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ? fecha : null;
    }

    public static int? Entero(string? valor) =>
        int.TryParse(valor?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    public static string? Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
