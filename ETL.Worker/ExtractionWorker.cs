using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Application.Options;
using Microsoft.Extensions.Options;

namespace ETL.Worker;

/// <summary>
/// Servicio en segundo plano (Worker Service) que ejecuta la fase de Extracción del ETL.
/// Si Etl:IntervaloMinutos = 0 se ejecuta una vez y se detiene; si es mayor, se repite periódicamente.
/// </summary>
public sealed class ExtractionWorker : BackgroundService
{
    private readonly IExtractionOrchestrator _orquestador;
    private readonly IHostApplicationLifetime _cicloDeVida;
    private readonly EtlOptions _opciones;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(
        IExtractionOrchestrator orquestador,
        IHostApplicationLifetime cicloDeVida,
        IOptions<EtlOptions> opciones,
        ILogger<ExtractionWorker> logger)
    {
        _orquestador = orquestador;
        _cicloDeVida = cicloDeVida;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de extracción iniciado. Intervalo: {Intervalo}",
            _opciones.IntervaloMinutos > 0 ? $"{_opciones.IntervaloMinutos} min" : "ejecución única");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var resultados = await _orquestador.EjecutarAsync(stoppingToken);
                MostrarResumen(resultados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en el ciclo de extracción.");
            }

            if (_opciones.IntervaloMinutos <= 0)
                break;

            _logger.LogInformation("Próxima ejecución en {Minutos} minuto(s).", _opciones.IntervaloMinutos);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (_opciones.IntervaloMinutos <= 0)
        {
            _logger.LogInformation("Ejecución única finalizada. Deteniendo el servicio.");
            _cicloDeVida.StopApplication();
        }
    }

    private void MostrarResumen(IReadOnlyList<ResultadoExtraccion> resultados)
    {
        _logger.LogInformation("==================== RESUMEN DE EXTRACCIÓN ====================");
        foreach (var r in resultados)
        {
            _logger.LogInformation(
                "{Estado,-8} | {Fuente,-20} | {Tipo,-9} | Extraídos: {Extraidos,5} | Rechazados: {Rechazados,3} | {Duracion,6} ms | {Velocidad} reg/s",
                r.Exitoso ? "OK" : "ERROR",
                r.NombreFuente,
                r.TipoFuente,
                r.RegistrosExtraidos,
                r.RegistrosRechazados,
                r.DuracionMs,
                r.RegistrosPorSegundo);
        }
        _logger.LogInformation("===============================================================");
    }
}
