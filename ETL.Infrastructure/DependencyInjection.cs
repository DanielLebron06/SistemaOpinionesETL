using ETL.Application.Abstractions;
using ETL.Infrastructure.Extractors;
using ETL.Infrastructure.Options;
using ETL.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ETL.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los extractores de forma modular: cada fuente se activa o desactiva
    /// con "Habilitado" en appsettings.json, sin tocar el código del orquestador.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuracion,
        bool soloDimensiones = false)
    {
        services.AddOptions<StagingOptions>()
            .Bind(configuracion.GetSection(StagingOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (soloDimensiones)
        {
            services.AddSingleton<IDimensionLoader, SqlDimensionLoader>();
            return services;
        }

        services.AddSingleton<IStagingRepository, SqlStagingRepository>();

        // --- Fuente 1: CSV (encuestas internas) ---
        if (configuracion.GetValue<bool>($"{CsvSourceOptions.Seccion}:Habilitado"))
        {
            services.AddOptions<CsvSourceOptions>()
                .Bind(configuracion.GetSection(CsvSourceOptions.Seccion))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IExtractor, CsvExtractor>();
        }

        // --- Fuente 2: Base de datos relacional (reseñas web) ---
        if (configuracion.GetValue<bool>($"{DatabaseSourceOptions.Seccion}:Habilitado"))
        {
            services.AddOptions<DatabaseSourceOptions>()
                .Bind(configuracion.GetSection(DatabaseSourceOptions.Seccion))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IExtractor, DatabaseExtractor>();
        }

        // --- Fuente 3: API REST (comentarios de redes sociales) ---
        if (configuracion.GetValue<bool>($"{ApiSourceOptions.Seccion}:Habilitado"))
        {
            services.AddOptions<ApiSourceOptions>()
                .Bind(configuracion.GetSection(ApiSourceOptions.Seccion))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                    "Falta la clave de la API (Fuentes:ComentariosApi:ApiKey). Configúrela con 'dotnet user-secrets' o una variable de entorno.")
                .ValidateOnStart();

            services.AddHttpClient(ApiExtractor.NombreHttpClient, (sp, cliente) =>
                {
                    var opciones = sp.GetRequiredService<IOptions<ApiSourceOptions>>().Value;
                    cliente.BaseAddress = new Uri(opciones.BaseUrl.TrimEnd('/') + "/");
                    cliente.DefaultRequestHeaders.Add("X-Api-Key", opciones.ApiKey);
                    cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                })
                // Reintentos con backoff exponencial, timeout por intento y circuit breaker.
                .AddStandardResilienceHandler();

            services.AddSingleton<IExtractor, ApiExtractor>();
        }

        return services;
    }
}
