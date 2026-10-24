using ETL.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETL.Data.Services;

public class OpinionService
{
    private const int BatchSize = 500;
    private const string IdClienteAnonimo = "ANONIMO";

    private readonly SistemaOpinionesClientesContext _context;

    public OpinionService(SistemaOpinionesClientesContext context)
    {
        _context = context;
    }

    public Task<(int procesados, int insertados, int rechazados)> CargarEncuestasAsync(
        string rutaCsv, string idFuenteDefault, int idPositivo, int idNeutral, int idNegativo)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "Encuestas",
            (string[] fields, string idFuente, out OpinionesCliente? op, out string? reason)
                => ParseEncuesta(fields, idFuente, idPositivo, idNeutral, idNegativo, out op, out reason));

    public Task<(int procesados, int insertados, int rechazados)> CargarComentariosSocialesAsync(
        string rutaCsv, string idFuenteDefault, int idNeutral)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "ComentariosSociales",
            (string[] fields, string idFuente, out OpinionesCliente? op, out string? reason)
                => ParseComentarioSocial(fields, idFuente, idNeutral, out op, out reason));

    public Task<(int procesados, int insertados, int rechazados)> CargarResenasWebAsync(
        string rutaCsv, string idFuenteDefault, int idPositivo, int idNeutral, int idNegativo)
        => CargarArchivoOpinionesAsync(
            rutaCsv,
            idFuenteDefault,
            "ResenasWeb",
            (string[] fields, string idFuente, out OpinionesCliente? op, out string? reason)
                => ParseResenaWeb(fields, idFuente, idPositivo, idNeutral, idNegativo, out op, out reason));

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

        var clienteIds = (await _context.Clientes.AsNoTracking()
            .Select(c => c.IdCliente)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var productoIds = (await _context.Productos.AsNoTracking()
            .Select(p => p.IdProducto)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fuenteIds = (await _context.FuentesDatos.AsNoTracking()
            .Select(f => f.IdFuente)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!fuenteIds.Contains(idFuenteDefault))
        {
            Console.WriteLine($"[{logLabel}] La fuente '{idFuenteDefault}' no existe en FuentesDatos.");
            return (0, 0, 0);
        }

        var clavesExistentes = await _context.OpinionesClientes.AsNoTracking()
            .Select(o => new { o.IdCliente, o.IdProducto, o.Fecha, o.Comentario })
            .ToListAsync();

        var clavesOpinion = clavesExistentes
            .Select(o => CrearClaveOpinion(o.IdCliente, o.IdProducto, o.Fecha, o.Comentario))
            .ToHashSet(StringComparer.Ordinal);

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

                EncolarClienteSiFalta(clienteIds, opinion.IdCliente);
                EncolarProductoSiFalta(productoIds, opinion.IdProducto);

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
            await GuardarMaestrosPendientesAsync();
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
            await RecargarIdsMaestrosAsync(clienteIds, productoIds);

            int ok = 0, fail = 0;
            foreach (var opinion in lote)
            {
                try
                {
                    EncolarClienteSiFalta(clienteIds, opinion.IdCliente);
                    EncolarProductoSiFalta(productoIds, opinion.IdProducto);
                    await GuardarMaestrosPendientesAsync();
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

    private async Task GuardarMaestrosPendientesAsync()
    {
        var hayMaestrosNuevos =
            _context.ChangeTracker.Entries<Cliente>().Any(e => e.State == EntityState.Added)
            || _context.ChangeTracker.Entries<Producto>().Any(e => e.State == EntityState.Added);

        if (hayMaestrosNuevos)
            await _context.SaveChangesAsync();
    }

    private async Task RecargarIdsMaestrosAsync(HashSet<string> clienteIds, HashSet<string> productoIds)
    {
        clienteIds.Clear();
        productoIds.Clear();

        foreach (var id in await _context.Clientes.AsNoTracking().Select(c => c.IdCliente).ToListAsync())
            clienteIds.Add(id);

        foreach (var id in await _context.Productos.AsNoTracking().Select(p => p.IdProducto).ToListAsync())
            productoIds.Add(id);
    }

    private void EncolarClienteSiFalta(HashSet<string> idsConocidos, string? idCliente)
    {
        if (idCliente is null || idsConocidos.Contains(idCliente))
            return;

        _context.Clientes.Add(new Cliente
        {
            IdCliente = idCliente,
            Nombre = $"Cliente_{idCliente}",
            Email = $"cliente_{idCliente}@mail.com"
        });
        idsConocidos.Add(idCliente);
    }

    private void EncolarProductoSiFalta(HashSet<string> idsConocidos, string idProducto)
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

    private static bool ParseEncuesta(
        string[] p, string idFuente,
        int idPositivo, int idNeutral, int idNegativo,
        out OpinionesCliente? opinion, out string? rejectReason)
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
            IdTipoFeedback = MapearTipoFeedback(clasificacion, idPositivo, idNeutral, idNegativo),
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = puntaje,
            Clasificacion = clasificacion
        };

        return true;
    }

    private static bool ParseComentarioSocial(
        string[] p, string idFuente, int idNeutral,
        out OpinionesCliente? opinion, out string? rejectReason)
    {
        opinion = null;
        rejectReason = null;

        if (p.Length < 6)
        {
            rejectReason = "columnas insuficientes";
            return false;
        }

        var idProducto = CsvLineParser.NormalizarId(p[2]);

        if (string.IsNullOrEmpty(idProducto))
        {
            rejectReason = "producto vacío";
            return false;
        }

        // Si IdCliente viene vacío (comentario anónimo en redes sociales), usamos centinela
        var idCliente = CsvLineParser.NormalizarId(p[1]);
        if (string.IsNullOrEmpty(idCliente))
            idCliente = IdClienteAnonimo;

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
            IdTipoFeedback = idNeutral,
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = 3,
            Clasificacion = "Neutral"
        };

        return true;
    }

    private static bool ParseResenaWeb(
        string[] p, string idFuente,
        int idPositivo, int idNeutral, int idNegativo,
        out OpinionesCliente? opinion, out string? rejectReason)
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
            IdTipoFeedback = rating >= 4 ? idPositivo : (rating <= 2 ? idNegativo : idNeutral),
            Fecha = fecha,
            Comentario = comentario,
            Puntaje = rating,
            Clasificacion = clasificacion
        };

        return true;
    }

    private static int MapearTipoFeedback(string clasificacion, int idPositivo, int idNeutral, int idNegativo)
    {
        var c = clasificacion.Trim().ToLowerInvariant();
        if (c.Contains("pos"))
            return idPositivo;
        if (c.Contains("neg"))
            return idNegativo;
        return idNeutral;
    }
}
