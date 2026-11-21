using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Domain.Entities;
using ETL.Infrastructure.Common;
using ETL.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure.Extractors;

/// <summary>
/// Extrae las encuestas internas desde un archivo CSV usando CsvHelper.
/// Lee el archivo en streaming (línea a línea) y valida cada fila antes de enviarla a staging.
/// </summary>
public sealed class CsvExtractor : IExtractor
{
    private readonly CsvSourceOptions _opciones;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<CsvSourceOptions> opciones, ILogger<CsvExtractor> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    public string NombreFuente => _opciones.NombreFuente;
    public string TipoFuente => Domain.Entities.TipoFuente.Csv;

    public async IAsyncEnumerable<OpinionExtraida> ExtraerAsync(
        EstadisticasExtraccion estadisticas,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var ruta = ResolverRuta(_opciones.Ruta);
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo CSV de la fuente '{NombreFuente}'.", ruta);

        _logger.LogInformation("[{Fuente}] Leyendo archivo {Archivo}", NombreFuente, Path.GetFileName(ruta));

        var configuracion = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _opciones.Delimitador,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = contexto =>
            {
                estadisticas.RegistrarRechazo();
                _logger.LogWarning("[{Fuente}] Fila con formato inválido en línea {Linea}", NombreFuente, contexto.Context.Parser?.RawRow);
            }
        };

        await using var stream = new FileStream(ruta, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        using var lector = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(lector, configuracion);

        // Valida que el encabezado tenga las columnas esperadas (lanza HeaderValidationException si no).
        await csv.ReadAsync();
        csv.ReadHeader();
        csv.ValidateHeader<EncuestaCsvRecord>();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var opinion = Convertir(csv, estadisticas);
            if (opinion is not null)
                yield return opinion;
        }
    }

    private OpinionExtraida? Convertir(CsvReader csv, EstadisticasExtraccion estadisticas)
    {
        var linea = csv.Parser.RawRow;
        try
        {
            var fila = csv.GetRecord<EncuestaCsvRecord>();
            var fecha = ParseoSeguro.Fecha(fila.Fecha);

            if (string.IsNullOrWhiteSpace(fila.IdOpinion) || string.IsNullOrWhiteSpace(fila.IdProducto)
                || string.IsNullOrWhiteSpace(fila.Comentario) || fecha is null)
            {
                estadisticas.RegistrarRechazo();
                _logger.LogWarning("[{Fuente}] Línea {Linea} rechazada: faltan campos obligatorios o la fecha es inválida.", NombreFuente, linea);
                return null;
            }

            return new OpinionExtraida
            {
                TipoFuente = TipoFuente,
                NombreFuente = NombreFuente,
                IdOrigen = fila.IdOpinion.Trim(),
                IdCliente = ParseoSeguro.Texto(fila.IdCliente),
                IdProducto = ParseoSeguro.Texto(fila.IdProducto),
                Fecha = fecha,
                Comentario = fila.Comentario.Trim(),
                Clasificacion = ParseoSeguro.Texto(fila.Clasificacion),
                Puntaje = ParseoSeguro.Entero(fila.PuntajeSatisfaccion),
                Canal = ParseoSeguro.Texto(fila.Fuente)
            };
        }
        catch (Exception ex)
        {
            estadisticas.RegistrarRechazo();
            _logger.LogWarning("[{Fuente}] Línea {Linea} rechazada: {Mensaje}", NombreFuente, linea, ex.Message);
            return null;
        }
    }

    private static string ResolverRuta(string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);

    /// <summary>Estructura del archivo surveys_part1.csv.</summary>
    private sealed class EncuestaCsvRecord
    {
        [Name("IdOpinion")] public string IdOpinion { get; set; } = string.Empty;
        [Name("IdCliente")] public string? IdCliente { get; set; }
        [Name("IdProducto")] public string IdProducto { get; set; } = string.Empty;
        [Name("Fecha")] public string? Fecha { get; set; }
        [Name("Comentario")] public string Comentario { get; set; } = string.Empty;
        [Name("Clasificación", "Clasificacion")] public string? Clasificacion { get; set; }
        [Name("PuntajeSatisfacción", "PuntajeSatisfaccion")] public string? PuntajeSatisfaccion { get; set; }
        [Name("Fuente")] public string? Fuente { get; set; }
    }
}
