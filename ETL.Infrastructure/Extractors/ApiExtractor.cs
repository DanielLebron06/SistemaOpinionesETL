using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using ETL.Application.Abstractions;
using ETL.Application.Models;
using ETL.Domain.Entities;
using ETL.Infrastructure.Common;
using ETL.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure.Extractors;

/// <summary>
/// Consume la API REST de comentarios de redes sociales mediante IHttpClientFactory.
/// Recorre la API página por página (paginación) y entrega cada comentario en streaming.
/// La autenticación (X-Api-Key) y la política de reintentos se configuran en el registro del HttpClient.
/// </summary>
public sealed class ApiExtractor : IExtractor
{
    public const string NombreHttpClient = "ComentariosApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiSourceOptions _opciones;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(IHttpClientFactory httpClientFactory, IOptions<ApiSourceOptions> opciones, ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public string NombreFuente => _opciones.NombreFuente;
    public string TipoFuente => Domain.Entities.TipoFuente.Api;

    public async IAsyncEnumerable<OpinionExtraida> ExtraerAsync(
        EstadisticasExtraccion estadisticas,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cliente = _httpClientFactory.CreateClient(NombreHttpClient);
        var pagina = 1;
        int totalPaginas;

        do
        {
            var url = $"{_opciones.Endpoint}?pagina={pagina}&tamanoPagina={_opciones.TamanoPagina}";
            _logger.LogInformation("[{Fuente}] GET {Url}", NombreFuente, new Uri(cliente.BaseAddress!, url));

            using var respuesta = await cliente.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"La API respondió {(int)respuesta.StatusCode} {respuesta.ReasonPhrase} en la página {pagina}.",
                    null, respuesta.StatusCode);

            var contenido = await respuesta.Content.ReadFromJsonAsync<PaginaComentariosDto>(cancellationToken)
                ?? throw new InvalidOperationException($"La API devolvió una respuesta vacía en la página {pagina}.");

            totalPaginas = contenido.TotalPaginas;

            foreach (var dto in contenido.Datos)
            {
                var opinion = Convertir(dto, estadisticas);
                if (opinion is not null)
                    yield return opinion;
            }

            pagina++;
        }
        while (pagina <= totalPaginas);
    }

    private OpinionExtraida? Convertir(ComentarioDto dto, EstadisticasExtraccion estadisticas)
    {
        var fecha = ParseoSeguro.Fecha(dto.Fecha);

        if (string.IsNullOrWhiteSpace(dto.IdComment) || string.IsNullOrWhiteSpace(dto.IdProducto)
            || string.IsNullOrWhiteSpace(dto.Comentario) || fecha is null)
        {
            estadisticas.RegistrarRechazo();
            _logger.LogWarning("[{Fuente}] Comentario {Id} rechazado: faltan campos obligatorios o la fecha es inválida.",
                NombreFuente, dto.IdComment);
            return null;
        }

        return new OpinionExtraida
        {
            TipoFuente = TipoFuente,
            NombreFuente = NombreFuente,
            IdOrigen = dto.IdComment.Trim(),
            IdCliente = ParseoSeguro.Texto(dto.IdCliente),
            IdProducto = dto.IdProducto.Trim(),
            Fecha = fecha,
            Comentario = dto.Comentario.Trim(),
            Clasificacion = null,
            Puntaje = null,
            Canal = ParseoSeguro.Texto(dto.Fuente)
        };
    }

    private sealed record PaginaComentariosDto(int Pagina, int TamanoPagina, int TotalRegistros, int TotalPaginas, List<ComentarioDto> Datos);

    private sealed record ComentarioDto(string IdComment, string? IdCliente, string IdProducto, string? Fuente, string? Fecha, string Comentario);
}
