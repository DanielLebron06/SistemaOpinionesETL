using ETL.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETL.Data.Services;

public class ClienteService
{
    private readonly SistemaOpinionesClientesContext _context;

    public ClienteService(SistemaOpinionesClientesContext context)
    {
        _context = context;
    }

    public async Task<(int procesados, int insertados, int rechazados)> CargarClientesAsync(string rutaCsv)
    {
        int procesados = 0, insertados = 0, rechazados = 0;

        if (!File.Exists(rutaCsv))
        {
            Console.WriteLine($"[Error] No se encontró el archivo en la ruta: {rutaCsv}");
            return (0, 0, 0);
        }

        var idsExistentes = (await _context.Clientes.AsNoTracking()
            .Select(c => c.IdCliente)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nuevos = new List<Cliente>();

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

                var idCliente = CsvLineParser.NormalizarId(partes[0]) ?? partes[0].Trim();
                var nombre = partes[1].Trim();
                var email = partes[2].Trim();

                if (string.IsNullOrEmpty(idCliente) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(email))
                {
                    rechazados++;
                    continue;
                }

                if (!idsExistentes.Add(idCliente))
                {
                    rechazados++;
                    continue;
                }

                nuevos.Add(new Cliente
                {
                    IdCliente = idCliente,
                    Nombre = nombre,
                    Email = email
                });
            }
            catch (Exception ex)
            {
                rechazados++;
                Console.WriteLine($"[Clientes] Fila {procesados} rechazada: {ex.Message}");
            }
        }

        if (nuevos.Count > 0)
        {
            await _context.Clientes.AddRangeAsync(nuevos);
            await _context.SaveChangesAsync();
            insertados = nuevos.Count;
        }

        Console.WriteLine($"[Clientes] Procesados: {procesados} | Insertados: {insertados} | Rechazados: {rechazados}");
        return (procesados, insertados, rechazados);
    }
}
