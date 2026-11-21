using System.Data;
using System.Runtime.CompilerServices;
using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Domain.Entities;
using ETL.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure.Extractors;

/// <summary>
/// Extrae las reseñas web desde la base de datos relacional transaccional (SistemaOpinionesClientes)
/// con ADO.NET. Usa una consulta parametrizada (evita inyección SQL) y un SqlDataReader en modo
/// secuencial para leer en streaming sin cargar toda la tabla en memoria.
/// </summary>
public sealed class DatabaseExtractor : IExtractor
{
    private const string Consulta = """
        SELECT  o.IdOpinion,
                o.IdCliente,
                o.IdProducto,
                o.Fecha,
                CAST(o.Comentario AS NVARCHAR(MAX)) AS Comentario,
                o.Clasificacion,
                o.Puntaje,
                f.TipoFuente
        FROM    dbo.OpinionesClientes AS o
        INNER JOIN dbo.FuentesDatos   AS f ON f.IdFuente = o.IdFuente
        WHERE   o.CodigoOrigen = @CodigoOrigen
        ORDER BY o.IdOpinion;
        """;

    private readonly DatabaseSourceOptions _opciones;
    private readonly string _cadenaConexion;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(
        IOptions<DatabaseSourceOptions> opciones,
        IConfiguration configuracion,
        ILogger<DatabaseExtractor> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"No existe la cadena de conexión '{_opciones.ConnectionStringName}' para la fuente '{_opciones.NombreFuente}'.");
    }

    public string NombreFuente => _opciones.NombreFuente;
    public string TipoFuente => Domain.Entities.TipoFuente.BaseDatos;

    public async IAsyncEnumerable<OpinionExtraida> ExtraerAsync(
        EstadisticasExtraccion estadisticas,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        _logger.LogInformation("[{Fuente}] Conectado a {BaseDatos}. Consultando reseñas con CodigoOrigen = {Codigo}",
            NombreFuente, conexion.Database, _opciones.CodigoOrigen);

        await using var comando = new SqlCommand(Consulta, conexion) { CommandTimeout = _opciones.TimeoutSegundos };
        comando.Parameters.Add("@CodigoOrigen", SqlDbType.VarChar, 20).Value = _opciones.CodigoOrigen;

        await using var lector = await comando.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            // Con SequentialAccess las columnas se leen en orden.
            var idOpinion = lector.GetInt32(0);
            var idCliente = await lector.IsDBNullAsync(1, cancellationToken) ? null : lector.GetString(1);
            var idProducto = lector.GetString(2);
            var fecha = lector.GetDateTime(3);
            var comentario = lector.GetString(4);
            var clasificacion = await lector.IsDBNullAsync(5, cancellationToken) ? null : lector.GetString(5);
            var puntaje = await lector.IsDBNullAsync(6, cancellationToken) ? (int?)null : lector.GetInt32(6);
            var canal = lector.GetString(7);

            if (string.IsNullOrWhiteSpace(comentario))
            {
                estadisticas.RegistrarRechazo();
                _logger.LogWarning("[{Fuente}] Reseña {Id} rechazada: comentario vacío.", NombreFuente, idOpinion);
                continue;
            }

            yield return new OpinionExtraida
            {
                TipoFuente = TipoFuente,
                NombreFuente = NombreFuente,
                IdOrigen = idOpinion.ToString(),
                IdCliente = idCliente,
                IdProducto = idProducto,
                Fecha = fecha,
                Comentario = comentario,
                Clasificacion = clasificacion,
                Puntaje = puntaje,
                Canal = canal
            };
        }
    }
}
