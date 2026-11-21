using ETL.Application.Models;
using ETL.Domain.Entities;

namespace ETL.Application.Abstractions;

/// <summary>
/// Contrato común para cualquier fuente de datos del proceso de extracción.
/// Agregar una nueva fuente = crear una clase que implemente esta interfaz y registrarla
/// (principio Abierto/Cerrado: el orquestador no se modifica).
/// </summary>
public interface IExtractor
{
    /// <summary>Nombre lógico de la fuente (se guarda en staging y en los logs).</summary>
    string NombreFuente { get; }

    /// <summary>Tipo técnico de la fuente (CSV, BaseDatos, API).</summary>
    string TipoFuente { get; }

    /// <summary>
    /// Extrae los registros como un flujo asíncrono (streaming): no se carga el origen completo
    /// en memoria, lo que permite procesar grandes volúmenes por lotes.
    /// </summary>
    IAsyncEnumerable<OpinionExtraida> ExtraerAsync(EstadisticasExtraccion estadisticas, CancellationToken cancellationToken);
}
