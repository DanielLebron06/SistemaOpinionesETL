using ETL.Application.Models;
using ETL.Domain.Entities;

namespace ETL.Application.Abstractions;

/// <summary>
/// Persistencia del área de staging. La capa de aplicación no conoce SQL Server:
/// solo depende de esta abstracción (Inversión de Dependencias).
/// </summary>
public interface IStagingRepository
{
    /// <summary>Elimina los registros previos de una fuente para que la extracción sea idempotente.</summary>
    Task LimpiarFuenteAsync(string nombreFuente, CancellationToken cancellationToken);

    /// <summary>Inserta un lote de registros extraídos y devuelve cuántos se guardaron.</summary>
    Task<int> GuardarLoteAsync(Guid loteId, IReadOnlyCollection<OpinionExtraida> registros, CancellationToken cancellationToken);

    /// <summary>Guarda la bitácora de la ejecución (auditoría y métricas de rendimiento).</summary>
    Task RegistrarEjecucionAsync(ResultadoExtraccion resultado, CancellationToken cancellationToken);
}
