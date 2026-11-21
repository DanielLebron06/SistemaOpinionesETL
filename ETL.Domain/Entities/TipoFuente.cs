namespace ETL.Domain.Entities;

/// <summary>Tipos de fuente soportados por el proceso de extracción.</summary>
public static class TipoFuente
{
    public const string Csv = "CSV";
    public const string BaseDatos = "BaseDatos";
    public const string Api = "API";
}
