using ETL.Application.Models;

namespace ETL.Application.Abstractions;

/// <summary>Caso de uso: ejecutar la extracción de todas las fuentes habilitadas hacia staging.</summary>
public interface IExtractionOrchestrator
{
    Task<IReadOnlyList<ResultadoExtraccion>> EjecutarAsync(CancellationToken cancellationToken);
}
