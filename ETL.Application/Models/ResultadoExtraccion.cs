namespace ETL.Application.Models;

/// <summary>Resultado y métricas de la extracción de una fuente.</summary>
public sealed record ResultadoExtraccion(
    Guid LoteId,
    string NombreFuente,
    string TipoFuente,
    DateTime InicioUtc,
    DateTime FinUtc,
    long DuracionMs,
    int RegistrosExtraidos,
    int RegistrosRechazados,
    bool Exitoso,
    string? MensajeError)
{
    public double RegistrosPorSegundo =>
        DuracionMs <= 0 ? RegistrosExtraidos : Math.Round(RegistrosExtraidos / (DuracionMs / 1000d), 1);
}
