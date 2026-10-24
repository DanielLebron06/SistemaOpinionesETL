using ETL.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETL.Data.Services;

public class OpinionService
{
    private const int BatchSize = 500;

    private readonly SistemaOpinionesClientesContext _context;

    public OpinionService(SistemaOpinionesClientesContext context)
    {
        _context = context;
    }

    public Task<(int procesados, int insertados, int rechazados)> CargarEncuestasAsync(string rutaCsv, string idFuenteDefault)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "Encuestas",
            ParseEncuesta);

    public Task<(int procesados, int insertados, int rechazados)> CargarComentariosSocialesAsync(string rutaCsv, string idFuenteDefault)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "ComentariosSociales",
            ParseComentarioSocial);

    public Task<(int procesados, int insertados, int rechazados)> CargarResenasWebAsync(string rutaCsv, string idFuenteDefault)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "ResenasWeb",
            ParseResenaWeb);

    private delegate bool OpinionRowParser(string[] fields, string idFuente, out OpinionesCliente? opinion, out string? rejectReason);

    private async Task<(int procesados, int insertados, int rechazados)> CargarArchivoOpinionesAsync(
        string rutaCsv,
        string idFuenteDefault,
        string logLabel,
        OpinionRowParser parser)
    {
        int procesados = 0, insertados = 0, rechazados = 0;

        if (!File.Exists(rutaCsv))
        {
            Console.WriteLine($"[Error] No se encontró el archivo: {rutaCsv}");
            return (0, 0, 0);
        }

        if (string.IsNullOrWhiteSpace(idFuenteDefault))
        {
            Console.WriteLine($"[{logLabel}] IdFuente no válido. Se aborta la carga.");
            return (0, 0, 0);
        }

        var clienteIds = await _context.Clientes.AsNoTracking()
            .Select(c => c.IdCliente)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        var productoIds = await _context.Productos.AsNoTracking()
            .Select(p => p.IdProducto)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        var fuenteIds = await _context.FuentesDatos.AsNoTracking()
            .Select(f => f.IdFuente)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        if (!fuenteIds.Contains(idFuenteDefault))
        {
            Console.WriteLine($"[{logLabel}] La fuente '{idFuenteDefault}' no existe en FuentesDatos.");
            return (0, 0, 0);
        }

        var clavesExistentes = await _context.OpinionesClientes.AsNoTracking()
            .Select(o => new { o.IdCliente, o.IdProducto, o.Fecha, o.Comentario })
            .ToListAsync();

        var clavesOpinion = new HashSet<string>(
            clavesExistentes.Select(CrearClaveOpinion),
            StringComparer.Ordinal);

        var lote = new List<OpinionesCliente>(BatchSize);
        var clavesLote = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linea in File.ReadLines(rutaCsv).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            procesados++;

            try
            {
                var campos = CsvLineParser.SplitFields(linea);

                if (!parser(campos, idFuenteDefault, out var opinion, out _))
                {
                    rechazados++;
                    continue;
                }

                if (opinion!.IdCliente != null && opinion.IdCliente.Length > 20)
                    opinion.IdCliente = opinion.IdCliente[..20];
                if (opinion.IdProducto.Length > 20)
                    opinion.IdProducto = opinion.IdProducto[..20];

                AsegurarCliente(clienteIds, opinion.IdCliente);
                AsegurarProducto(productoIds, opinion.IdProducto);

                var clave = CrearClaveOpinion(opinion);
                if (!clavesOpinion.Add(clave) || !clavesLote.Add(clave))
                {
                    rechazados++;
                    continue;
                }

                lote.Add(opinion);

                if (lote.Count >= BatchSize)
                {
                    var (ok, fail) = await PersistirLoteAsync(lote, clavesLote, clavesOpinion, clienteIds, productoIds);
                    insertados += ok;
                    rechazados += fail;
                }
            }
            catch (Exception ex)
            {
                rechazados++;
                Console.WriteLine($"[{logLabel}] Fila {procesados} rechazada: {ex.Message}");
            }
        }

        if (lote.Count > 0)
        {
            var (ok, fail) = await PersistirLoteAsync(lote, clavesLote, clavesOpinion, clienteIds, productoIds);
            insertados += ok;
            rechazados += fail;
        }

        Console.WriteLine($"[{logLabel}] Procesados: {procesados} | Insertados: {insertados} | Rechazados: {rechazados}");
        return (procesados, insertados, rechazados);
    }

    private async Task<(int insertados, int rechazados)> PersistirLoteAsync(
        List<OpinionesCliente> lote,
        HashSet<string> clavesLote,
        HashSet<string> clavesOpinion,
        HashSet<string> clienteIds,
        HashSet<string> productoIds)
    {
        if (lote.Count == 0)
            return (0, 0);

        try
        {
            await _context.OpinionesClientes.AddRangeAsync(lote);
            await _context.SaveChangesAsync();
            var count = lote.Count;
            lote.Clear();
            clavesLote.Clear();
            _context.ChangeTracker.Clear();
            return (count, 0);
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"[Opiniones] Lote fallido ({lote.Count} filas), reintento fila a fila: {ex.InnerException?.Message ?? ex.Message}");
            _context.ChangeTracker.Clear();

            int ok = 0, fail = 0;
            foreach (var opinion in lote)
            {
                try
                {
                    AsegurarCliente(clienteIds, opinion.IdCliente);
                    AsegurarProducto(productoIds, opinion.IdProducto);
                    _context.OpinionesClientes.Add(opinion);
                    await _context.SaveChangesAsync();
                    ok++;
                    _context.ChangeTracker.Clear();
                }
                catch
                {
                    fail++;
                    clavesOpinion.Remove(CrearClaveOpinion(opinion));
                    _context.ChangeTracker.Clear();
                }
            }

            lote.Clear();
            clavesLote.Clear();
            return (ok, fail);
        }
    }

    private void AsegurarCliente(HashSet<string> idsConocidos, string idCliente)
    {
        if (idsConocidos.Contains(idCliente))
            return;

        _context.Clientes.Add(new Cliente
        {
            IdCliente = idCliente,
            Nombre = $"Cliente_{idCliente}",
            Email = $"cliente_{idCliente}@mail.com"
        });
        idsConocidos.Add(idCliente);
    }

    private void AsegurarProducto(HashSet<string> idsConocidos, string idProducto)
    {
        if (idsConocidos.Contains(idProducto))
            return;

        _context.Productos.Add(new Producto
        {
            IdProducto = idProducto,
            Nombre = $"Producto_{idProducto}",
            Categoria = "General"
        });
        idsConocidos.Add(idProducto);
    }

    private static string CrearClaveOpinion(OpinionesCliente o)
        => CrearClaveOpinion(o.IdCliente, o.IdProducto, o.Fecha, o.Comentario);

    private static string CrearClaveOpinion(string? idCliente, string idProducto, DateTime fecha, string comentario)
        => $"{idCliente ?? string.Empty}|{idProducto}|{fecha:O}|{comentario}";

    private static bool ParseEncuesta(string[] p, string idFuente, out OpinionesCliente? opinion, out string? rejectReason)
    {
        opinion = null;
        rejectReason = null;

        if (p.Length < 7)
        {
            rejectReason = "columnas insuficientes";
            return false;
        }

        var idCliente = CsvLineParser.NormalizarId(p[1]);
        var idProducto = CsvLineParser.NormalizarId(p[2]);

        if (string.IsNullOrEmpty(idCliente) || string.IsNullOrEmpty(idProducto))
        {
            rejectReason = "cliente o producto vacío";
            return false;
        }

        if (!CsvLineParser.TryParseDate(p[3], out var fecha))
        {
            rejectReason = "fecha inválida";
            return false;
        }

        var comentario = p[4].Trim();
        if (string.IsNullOrEmpty(comentario))
        {
            rejectReason = "comentario vacío";
            return false;
        }

        var clasificacion = p[5].Trim();
        if (!int.TryParse(p[6].Trim(), out var puntaje))
        {
            rejectReason = "puntaje inválido";
            return false;
        }

        opinion = new OpinionesCliente
        {
            CodigoOrigen = "Encuesta",
            IdCliente = idCliente,
            IdProducto = idProducto,
            IdFuente = idFuente,
            IdTipoFeedback = MapearTipoFeedback(clasificacion),
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = puntaje,
            Clasificacion = clasificacion
        };

        return true;
    }

    private static bool ParseComentarioSocial(string[] p, string idFuente, out OpinionesCliente? opinion, out string? rejectReason)
    {
        opinion = null;
        rejectReason = null;

        if (p.Length < 6)
        {
            rejectReason = "columnas insuficientes";
            return false;
        }

        var idCliente = CsvLineParser.NormalizarId(p[1]);
        var idProducto = CsvLineParser.NormalizarId(p[2]);

        if (string.IsNullOrEmpty(idCliente) || string.IsNullOrEmpty(idProducto))
        {
            rejectReason = "cliente o producto vacío";
            return false;
        }

        if (!CsvLineParser.TryParseDate(p[4], out var fecha))
        {
            rejectReason = "fecha inválida";
            return false;
        }

        var comentario = p[5].Trim();
        if (string.IsNullOrEmpty(comentario))
        {
            rejectReason = "comentario vacío";
            return false;
        }

        opinion = new OpinionesCliente
        {
            CodigoOrigen = "Social",
            IdCliente = idCliente,
            IdProducto = idProducto,
            IdFuente = idFuente,
            IdTipoFeedback = 2,
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = 3,
            Clasificacion = "Neutral"
        };

        return true;
    }

    private static bool ParseResenaWeb(string[] p, string idFuente, out OpinionesCliente? opinion, out string? rejectReason)
    {
        opinion = null;
        rejectReason = null;

        if (p.Length < 6)
        {
            rejectReason = "columnas insuficientes";
            return false;
        }

        var idCliente = CsvLineParser.NormalizarId(p[1]);
        var idProducto = CsvLineParser.NormalizarId(p[2]);

        if (string.IsNullOrEmpty(idCliente) || string.IsNullOrEmpty(idProducto))
        {
            rejectReason = "cliente o producto vacío";
            return false;
        }

        if (!CsvLineParser.TryParseDate(p[3], out var fecha))
        {
            rejectReason = "fecha inválida";
            return false;
        }

        var comentario = p[4].Trim();
        if (string.IsNullOrEmpty(comentario))
        {
            rejectReason = "comentario vacío";
            return false;
        }

        if (!int.TryParse(p[5].Trim(), out var rating))
        {
            rejectReason = "rating inválido";
            return false;
        }

        var clasificacion = rating >= 4 ? "Positivo" : (rating <= 2 ? "Negativo" : "Neutral");

        opinion = new OpinionesCliente
        {
            CodigoOrigen = "WebReview",
            IdCliente = idCliente,
            IdProducto = idProducto,
            IdFuente = idFuente,
            IdTipoFeedback = rating >= 4 ? 1 : (rating <= 2 ? 3 : 2),
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = rating,
            Clasificacion = clasificacion
        };

        return true;
    }

    private static int MapearTipoFeedback(string clasificacion)
    {
        var c = clasificacion.Trim().ToLowerInvariant();
        if (c.Contains("pos"))
            return 1;
        if (c.Contains("neg"))
            return 3;
        return 2;
    }
}
