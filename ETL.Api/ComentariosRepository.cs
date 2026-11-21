using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ETL.Api;

public sealed record ComentarioDto(string IdComment, string? IdCliente, string IdProducto, string? Fuente, string? Fecha, string Comentario);

public sealed record PaginaComentarios(int Pagina, int TamanoPagina, int TotalRegistros, int TotalPaginas, IReadOnlyList<ComentarioDto> Datos);

/// <summary>Origen de datos de la API: carga social_comments.csv una vez y lo sirve paginado.</summary>
public sealed class ComentariosRepository
{
    private readonly IReadOnlyList<ComentarioDto> _comentarios;

    public ComentariosRepository(IConfiguration configuracion, ILogger<ComentariosRepository> logger)
    {
        var ruta = configuracion["Datos:RutaComentarios"] ?? "data/social_comments.csv";
        if (!Path.IsPathRooted(ruta))
            ruta = Path.Combine(AppContext.BaseDirectory, ruta);

        using var lector = new StreamReader(ruta);
        using var csv = new CsvReader(lector, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null
        });

        _comentarios = csv.GetRecords<ComentarioDto>().ToList();
        logger.LogInformation("API: {Total} comentarios cargados desde {Archivo}", _comentarios.Count, Path.GetFileName(ruta));
    }

    public PaginaComentarios ObtenerPagina(int pagina, int tamanoPagina)
    {
        var total = _comentarios.Count;
        var totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);
        var datos = _comentarios.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
        return new PaginaComentarios(pagina, tamanoPagina, total, totalPaginas, datos);
    }
}
