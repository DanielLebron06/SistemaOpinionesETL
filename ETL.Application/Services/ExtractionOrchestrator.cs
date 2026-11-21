using System.Collections.Concurrent;
using System.Diagnostics;
using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Application.Options;
using ETL.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETL.Application.Services;

/// <summary>
/// Orquesta la fase de Extracción: ejecuta todos los extractores registrados en paralelo,
/// envía los datos a staging por lotes y registra métricas (Stopwatch) y errores (ILogger).
/// </summary>
public sealed class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly IReadOnlyList<IExtractor> _extractores;
    private readonly IStagingRepository _staging;
    private readonly EtlOptions _opciones;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IEnumerable<IExtractor> extractores,
        IStagingRepository staging,
        IOptions<EtlOptions> opciones,
        ILogger<ExtractionOrchestrator> logger)
    {
        _extractores = extractores.ToList();
        _staging = staging;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResultadoExtraccion>> EjecutarAsync(CancellationToken cancellationToken)
    {
        var loteId = Guid.NewGuid();
        var resultados = new ConcurrentBag<ResultadoExtraccion>();
        var cronometroTotal = Stopwatch.StartNew();

        if (_extractores.Count == 0)
        {
            _logger.LogWarning("No hay fuentes habilitadas en la configuración. No se extrae nada.");
            return Array.Empty<ResultadoExtraccion>();
        }

        _logger.LogInformation(
            "Inicio de extracción | Lote {LoteId} | Fuentes: {Fuentes} | Paralelismo: {Paralelismo} | Tamaño de lote: {TamanoLote}",
            loteId,
            string.Join(", ", _extractores.Select(e => $"{e.NombreFuente} ({e.TipoFuente})")),
            _opciones.MaxGradoParalelismo,
            _opciones.TamanoLote);

        var opcionesParalelo = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _opciones.MaxGradoParalelismo),
            CancellationToken = cancellationToken
        };

        // Rendimiento: cada fuente se extrae en su propia tarea asíncrona, en paralelo.
        await Parallel.ForEachAsync(_extractores, opcionesParalelo, async (extractor, ct) =>
        {
            resultados.Add(await ExtraerFuenteAsync(extractor, loteId, ct));
        });

        cronometroTotal.Stop();

        var lista = resultados.OrderBy(r => r.NombreFuente).ToList();
        _logger.LogInformation(
            "Fin de extracción | Lote {LoteId} | Registros totales: {Total} | Rechazados: {Rechazados} | Fuentes con error: {Errores} | Duración total: {Duracion} ms",
            loteId,
            lista.Sum(r => r.RegistrosExtraidos),
            lista.Sum(r => r.RegistrosRechazados),
            lista.Count(r => !r.Exitoso),
            cronometroTotal.ElapsedMilliseconds);

        return lista;
    }

    private async Task<ResultadoExtraccion> ExtraerFuenteAsync(IExtractor extractor, Guid loteId, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["Fuente"] = extractor.NombreFuente });

        var inicio = DateTime.UtcNow;
        var cronometro = Stopwatch.StartNew();
        var estadisticas = new EstadisticasExtraccion();
        var totalGuardados = 0;
        string? error = null;

        _logger.LogInformation("[{Fuente}] Iniciando extracción desde {TipoFuente}...", extractor.NombreFuente, extractor.TipoFuente);

        try
        {
            if (_opciones.LimpiarStagingAntesDeCargar)
                await _staging.LimpiarFuenteAsync(extractor.NombreFuente, ct);

            var buffer = new List<OpinionExtraida>(_opciones.TamanoLote);

            await foreach (var registro in extractor.ExtraerAsync(estadisticas, ct))
            {
                buffer.Add(registro);

                if (buffer.Count >= _opciones.TamanoLote)
                {
                    totalGuardados += await _staging.GuardarLoteAsync(loteId, buffer, ct);
                    _logger.LogDebug("[{Fuente}] Lote guardado en staging. Acumulado: {Acumulado}", extractor.NombreFuente, totalGuardados);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
                totalGuardados += await _staging.GuardarLoteAsync(loteId, buffer, ct);

            cronometro.Stop();
            _logger.LogInformation(
                "[{Fuente}] Extracción completada: {Registros} registros a staging, {Rechazados} rechazados, {Duracion} ms",
                extractor.NombreFuente, totalGuardados, estadisticas.Rechazados, cronometro.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("[{Fuente}] Extracción cancelada por apagado del servicio.", extractor.NombreFuente);
            throw;
        }
        catch (Exception ex)
        {
            // Un fallo en una fuente no detiene a las demás (tolerancia a fallos).
            cronometro.Stop();
            error = ex.Message;
            _logger.LogError(ex, "[{Fuente}] Error durante la extracción: {Mensaje}", extractor.NombreFuente, ex.Message);
        }

        var resultado = new ResultadoExtraccion(
            loteId,
            extractor.NombreFuente,
            extractor.TipoFuente,
            inicio,
            DateTime.UtcNow,
            cronometro.ElapsedMilliseconds,
            totalGuardados,
            estadisticas.Rechazados,
            error is null,
            error);

        try
        {
            await _staging.RegistrarEjecucionAsync(resultado, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Fuente}] No se pudo registrar la bitácora de ejecución.", extractor.NombreFuente);
        }

        return resultado;
    }
}
