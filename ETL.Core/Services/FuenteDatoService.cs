using ETL.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETL.Data.Services;

public class FuenteDatoService
{
    private readonly SistemaOpinionesClientesContext _context;

    public FuenteDatoService(SistemaOpinionesClientesContext context)
    {
        _context = context;
    }

    public async Task<(int procesados, int insertados, int rechazados)> CargarFuentesAsync(string rutaCsv)
    {
        int procesados = 0, insertados = 0, rechazados = 0;

        if (!File.Exists(rutaCsv))
        {
            Console.WriteLine($"[Error] No se encontró el archivo en la ruta: {rutaCsv}");
            return (0, 0, 0);
        }

        var idsExistentes = (await _context.FuentesDatos.AsNoTracking()
            .Select(f => f.IdFuente)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nuevos = new List<FuentesDato>();

        foreach (var linea in File.ReadLines(rutaCsv).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            procesados++;

            try
            {
                var partes = CsvLineParser.SplitFields(linea);
                if (partes.Length < 3)
                {
                    rechazados++;
                    continue;
                }

                var idFuente = partes[0].Trim();
                var tipoFuente = partes[1].Trim();

                if (!CsvLineParser.TryParseDate(partes[2], out var fechaCarga))
                {
                    rechazados++;
                    continue;
                }

                if (string.IsNullOrEmpty(idFuente) || string.IsNullOrEmpty(tipoFuente))
                {
                    rechazados++;
                    continue;
                }

                if (!idsExistentes.Add(idFuente))
                {
                    rechazados++;
                    continue;
                }

                nuevos.Add(new FuentesDato
                {
                    IdFuente = idFuente,
                    TipoFuente = tipoFuente,
                    FechaCarga = fechaCarga
                });
            }
            catch (Exception ex)
            {
                rechazados++;
                Console.WriteLine($"[FuentesDatos] Fila {procesados} rechazada: {ex.Message}");
            }
        }

        if (nuevos.Count > 0)
        {
            await _context.FuentesDatos.AddRangeAsync(nuevos);
            await _context.SaveChangesAsync();
            insertados = nuevos.Count;
        }

        Console.WriteLine($"[FuentesDatos] Procesados: {procesados} | Insertados: {insertados} | Rechazados: {rechazados}");
        return (procesados, insertados, rechazados);
    }
}
