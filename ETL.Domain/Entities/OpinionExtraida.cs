namespace ETL.Domain.Entities;

/// <summary>
/// Registro de opinión tal como fue extraído de cualquier fuente (CSV, BD relacional o API REST).
/// Es el modelo común que viaja desde los extractores hasta la tabla staging (stg.Opiniones).
/// No aplica transformaciones de negocio: eso corresponde a la fase T del ETL.
/// </summary>
public sealed class OpinionExtraida
{
    /// <summary>Tipo técnico de la fuente: CSV, BaseDatos o API.</summary>
    public required string TipoFuente { get; init; }

    /// <summary>Nombre lógico de la fuente configurada (ej. EncuestasInternas).</summary>
    public required string NombreFuente { get; init; }

    /// <summary>Identificador del registro en el sistema de origen (IdOpinion, IdReview, IdComment...).</summary>
    public required string IdOrigen { get; init; }

    public string? IdCliente { get; init; }
    public string? IdProducto { get; init; }
    public DateTime? Fecha { get; init; }
    public string? Comentario { get; init; }
    public string? Clasificacion { get; init; }
    public int? Puntaje { get; init; }

    /// <summary>Canal de origen informado por la fuente (EncuestaInterna, Instagram, Twitter...).</summary>
    public string? Canal { get; init; }
}
