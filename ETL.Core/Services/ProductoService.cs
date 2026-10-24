using ETL.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETL.Data.Services;

public class ProductoService
{
    private readonly SistemaOpinionesClientesContext _context;

    public ProductoService(SistemaOpinionesClientesContext context)
    {
        _context = context;
    }

    public async Task<(int procesados, int insertados, int rechazados)> CargarProductosAsync(string rutaCsv)
    {
        int procesados = 0, insertados = 0, rechazados = 0;

        if (!File.Exists(rutaCsv))
        {
            Console.WriteLine($"[Error] No se encontró el archivo en la ruta: {rutaCsv}");
            return (0, 0, 0);
        }

        var idsExistentes = (await _context.Productos.AsNoTracking()
            .Select(p => p.IdProducto)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nuevos = new List<Producto>();

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

                var idProducto = CsvLineParser.NormalizarId(partes[0]) ?? partes[0].Trim();
                var nombre = partes[1].Trim();
                var categoria = partes[2].Trim();

                if (string.IsNullOrEmpty(idProducto) || string.IsNullOrEmpty(nombre))
                {
                    rechazados++;
                    continue;
                }

                if (string.IsNullOrEmpty(categoria))
                    categoria = "General";

                if (!idsExistentes.Add(idProducto))
                {
                    rechazados++;
                    continue;
                }

                nuevos.Add(new Producto
                {
                    IdProducto = idProducto,
                    Nombre = nombre,
                    Categoria = categoria
                });
            }
            catch (Exception ex)
            {
                rechazados++;
                Console.WriteLine($"[Productos] Fila {procesados} rechazada: {ex.Message}");
            }
        }

        if (nuevos.Count > 0)
        {
            await _context.Productos.AddRangeAsync(nuevos);
            await _context.SaveChangesAsync();
            insertados = nuevos.Count;
        }

        Console.WriteLine($"[Productos] Procesados: {procesados} | Insertados: {insertados} | Rechazados: {rechazados}");
        return (procesados, insertados, rechazados);
    }
}
