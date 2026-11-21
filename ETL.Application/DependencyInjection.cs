using ETL.Application.Abstractions;
using ETL.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ETL.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IExtractionOrchestrator, ExtractionOrchestrator>();
        return services;
    }
}
