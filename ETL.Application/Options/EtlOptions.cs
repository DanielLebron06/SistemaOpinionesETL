namespace ETL.Application.Options;

/// <summary>Parámetros generales del proceso (sección "Etl" de appsettings.json).</summary>
public sealed class EtlOptions
{
    public const string Seccion = "Etl";

    /// <summary>Cantidad de registros por inserción masiva en staging.</summary>
    public int TamanoLote { get; set; } = 500;

    /// <summary>Cuántas fuentes se extraen en paralelo.</summary>
    public int MaxGradoParalelismo { get; set; } = 3;

    /// <summary>Minutos entre ejecuciones. 0 = ejecutar una sola vez y detener el servicio.</summary>
    public int IntervaloMinutos { get; set; } = 0;

    /// <summary>Si es true, borra los datos previos de cada fuente en staging antes de cargar.</summary>
    public bool LimpiarStagingAntesDeCargar { get; set; } = true;
}
