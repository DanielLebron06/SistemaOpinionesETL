namespace ETL.Application.Models;

/// <summary>
/// Contadores de una extracción. Thread-safe porque varias fuentes se ejecutan en paralelo.
/// </summary>
public sealed class EstadisticasExtraccion
{
    private int _rechazados;

    public int Rechazados => _rechazados;

    public void RegistrarRechazo() => Interlocked.Increment(ref _rechazados);
}
