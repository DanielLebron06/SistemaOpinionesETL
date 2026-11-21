using System.Security.Cryptography;
using System.Text;

namespace ETL.Api;

/// <summary>
/// Exige el encabezado X-Api-Key en cada petición. Compara en tiempo constante
/// para no filtrar información de la clave por diferencias de tiempo.
/// </summary>
public sealed class ApiKeyFilter : IEndpointFilter
{
    public const string Encabezado = "X-Api-Key";
    private readonly byte[] _claveEsperada;

    public ApiKeyFilter(string claveEsperada) => _claveEsperada = Encoding.UTF8.GetBytes(claveEsperada);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var recibida = context.HttpContext.Request.Headers[Encabezado].ToString();

        if (string.IsNullOrEmpty(recibida)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibida), _claveEsperada))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "API key inválida o ausente.");
        }

        return await next(context);
    }
}
