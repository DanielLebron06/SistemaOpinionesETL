using System.Data;
using ETL.Application.Abstractions;
using ETL.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure.Persistence;

public sealed class SqlDimensionLoader(
    IConfiguration configuracion,
    IOptions<StagingOptions> opciones,
    ILogger<SqlDimensionLoader> logger) : IDimensionLoader
{
    public async Task CargarAsync(CancellationToken cancellationToken)
    {
        var staging = opciones.Value;
        var cadenaConexion = configuracion.GetConnectionString(staging.ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la conexión '{staging.ConnectionStringName}'.");

        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        logger.LogInformation("Iniciando carga de dimensiones en {BaseDatos}.", conexion.Database);

        await using var comando = new SqlCommand("dw.CargarDimensiones", conexion)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = staging.TimeoutSegundos
        };
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            logger.LogInformation(
                "Ejecución {Ejecucion} | {Dimension} | Insertadas: {Insertadas} | Actualizadas: {Actualizadas} | Total: {Total}",
                lector.GetInt64(0), lector.GetString(1), lector.GetInt32(2), lector.GetInt32(3), lector.GetInt32(4));
        }

        logger.LogInformation("Carga de dimensiones completada.");
    }
}
