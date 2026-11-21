using System.ComponentModel.DataAnnotations;

namespace ETL.Infrastructure.Options;

/// <summary>Configuración de la fuente CSV (sección "Fuentes:EncuestasCsv").</summary>
public sealed class CsvSourceOptions
{
    public const string Seccion = "Fuentes:EncuestasCsv";

    public bool Habilitado { get; set; } = true;

    [Required] public string NombreFuente { get; set; } = "EncuestasInternas";

    /// <summary>Ruta absoluta o relativa a la carpeta del ejecutable.</summary>
    [Required] public string Ruta { get; set; } = string.Empty;

    public string Delimitador { get; set; } = ",";
}

/// <summary>Configuración de la fuente de base de datos relacional (sección "Fuentes:ResenasBaseDatos").</summary>
public sealed class DatabaseSourceOptions
{
    public const string Seccion = "Fuentes:ResenasBaseDatos";

    public bool Habilitado { get; set; } = true;

    [Required] public string NombreFuente { get; set; } = "ResenasWeb";

    /// <summary>Nombre de la cadena de conexión dentro de "ConnectionStrings".</summary>
    [Required] public string ConnectionStringName { get; set; } = "FuenteOltp";

    /// <summary>Valor de CodigoOrigen que identifica las reseñas en la BD transaccional.</summary>
    [Required] public string CodigoOrigen { get; set; } = "WebReview";

    [Range(5, 600)] public int TimeoutSegundos { get; set; } = 60;
}

/// <summary>Configuración de la fuente API REST (sección "Fuentes:ComentariosApi").</summary>
public sealed class ApiSourceOptions
{
    public const string Seccion = "Fuentes:ComentariosApi";

    public bool Habilitado { get; set; } = true;

    [Required] public string NombreFuente { get; set; } = "ComentariosSociales";

    [Required, Url] public string BaseUrl { get; set; } = string.Empty;

    [Required] public string Endpoint { get; set; } = "api/comentarios";

    [Range(1, 1000)] public int TamanoPagina { get; set; } = 50;

    /// <summary>
    /// Clave de la API. NO se guarda en appsettings.json: se inyecta con User Secrets
    /// (desarrollo) o variables de entorno (producción).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Configuración del área de staging (sección "Staging").</summary>
public sealed class StagingOptions
{
    public const string Seccion = "Staging";

    [Required] public string ConnectionStringName { get; set; } = "Staging";

    [Range(5, 600)] public int TimeoutSegundos { get; set; } = 120;
}
