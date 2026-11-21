using System.Data;
using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Domain.Entities;
using ETL.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure.Persistence;

/// <summary>
/// Guarda los datos extraídos en las tablas staging de la base analítica (esquema stg).
/// Usa SqlBulkCopy para insertar lotes completos en una sola operación (alto rendimiento).
/// </summary>
public sealed class SqlStagingRepository : IStagingRepository
{
    private const string TablaOpiniones = "stg.Opiniones";

    private readonly string _cadenaConexion;
    private readonly StagingOptions _opciones;

    public SqlStagingRepository(IOptions<StagingOptions> opciones, IConfiguration configuracion)
    {
        _opciones = opciones.Value;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{_opciones.ConnectionStringName}' para staging.");
    }

    public async Task LimpiarFuenteAsync(string nombreFuente, CancellationToken cancellationToken)
    {
        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        await using var comando = new SqlCommand(
            $"DELETE FROM {TablaOpiniones} WHERE NombreFuente = @NombreFuente;", conexion)
        {
            CommandTimeout = _opciones.TimeoutSegundos
        };
        comando.Parameters.Add("@NombreFuente", SqlDbType.VarChar, 50).Value = nombreFuente;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GuardarLoteAsync(Guid loteId, IReadOnlyCollection<OpinionExtraida> registros, CancellationToken cancellationToken)
    {
        if (registros.Count == 0)
            return 0;

        using var tabla = CrearTabla();
        var fechaExtraccion = DateTime.UtcNow;

        foreach (var r in registros)
        {
            tabla.Rows.Add(
                loteId,
                r.TipoFuente,
                r.NombreFuente,
                r.IdOrigen,
                (object?)r.IdCliente ?? DBNull.Value,
                (object?)r.IdProducto ?? DBNull.Value,
                (object?)r.Fecha ?? DBNull.Value,
                (object?)r.Comentario ?? DBNull.Value,
                (object?)r.Clasificacion ?? DBNull.Value,
                (object?)r.Puntaje ?? DBNull.Value,
                (object?)r.Canal ?? DBNull.Value,
                fechaExtraccion);
        }

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        using var bulk = new SqlBulkCopy(conexion)
        {
            DestinationTableName = TablaOpiniones,
            BatchSize = registros.Count,
            BulkCopyTimeout = _opciones.TimeoutSegundos
        };

        foreach (DataColumn columna in tabla.Columns)
            bulk.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);

        await bulk.WriteToServerAsync(tabla, cancellationToken);
        return registros.Count;
    }

    public async Task RegistrarEjecucionAsync(ResultadoExtraccion resultado, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO stg.ControlExtraccion
                (LoteId, NombreFuente, TipoFuente, InicioUtc, FinUtc, DuracionMs, RegistrosExtraidos, RegistrosRechazados, Estado, MensajeError)
            VALUES
                (@LoteId, @NombreFuente, @TipoFuente, @InicioUtc, @FinUtc, @DuracionMs, @Extraidos, @Rechazados, @Estado, @MensajeError);
            """;

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand(sql, conexion) { CommandTimeout = _opciones.TimeoutSegundos };

        comando.Parameters.Add("@LoteId", SqlDbType.UniqueIdentifier).Value = resultado.LoteId;
        comando.Parameters.Add("@NombreFuente", SqlDbType.VarChar, 50).Value = resultado.NombreFuente;
        comando.Parameters.Add("@TipoFuente", SqlDbType.VarChar, 20).Value = resultado.TipoFuente;
        comando.Parameters.Add("@InicioUtc", SqlDbType.DateTime2).Value = resultado.InicioUtc;
        comando.Parameters.Add("@FinUtc", SqlDbType.DateTime2).Value = resultado.FinUtc;
        comando.Parameters.Add("@DuracionMs", SqlDbType.BigInt).Value = resultado.DuracionMs;
        comando.Parameters.Add("@Extraidos", SqlDbType.Int).Value = resultado.RegistrosExtraidos;
        comando.Parameters.Add("@Rechazados", SqlDbType.Int).Value = resultado.RegistrosRechazados;
        comando.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = resultado.Exitoso ? "Exitoso" : "Fallido";
        comando.Parameters.Add("@MensajeError", SqlDbType.NVarChar, -1).Value = (object?)resultado.MensajeError ?? DBNull.Value;

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DataTable CrearTabla()
    {
        var tabla = new DataTable();
        tabla.Columns.Add("LoteId", typeof(Guid));
        tabla.Columns.Add("TipoFuente", typeof(string));
        tabla.Columns.Add("NombreFuente", typeof(string));
        tabla.Columns.Add("IdOrigen", typeof(string));
        tabla.Columns.Add("IdCliente", typeof(string));
        tabla.Columns.Add("IdProducto", typeof(string));
        tabla.Columns.Add("Fecha", typeof(DateTime));
        tabla.Columns.Add("Comentario", typeof(string));
        tabla.Columns.Add("Clasificacion", typeof(string));
        tabla.Columns.Add("Puntaje", typeof(int));
        tabla.Columns.Add("Canal", typeof(string));
        tabla.Columns.Add("FechaExtraccionUtc", typeof(DateTime));
        return tabla;
    }
}
