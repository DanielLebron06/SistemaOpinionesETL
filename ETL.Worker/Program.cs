using ETL.Application;
using ETL.Application.Abstractions;
using ETL.Application.Options;
using ETL.Infrastructure;
using ETL.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
var soloDimensiones = builder.Configuration.GetValue<bool>("Etl:SoloDimensiones");
if (soloDimensiones)
    builder.Configuration["Serilog:WriteTo:1:Args:path"] = "logs/etl-dimensiones-.log";

// Logging centralizado: ILogger<T> en todo el código, Serilog como proveedor (consola + archivo).
builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddInfrastructure(builder.Configuration, soloDimensiones);
if (!soloDimensiones)
{
    builder.Services.AddOptions<EtlOptions>()
        .Bind(builder.Configuration.GetSection(EtlOptions.Seccion))
        .ValidateOnStart();
    builder.Services.AddApplication();
    builder.Services.AddHostedService<ExtractionWorker>();
}

using var host = builder.Build();

try
{
    if (soloDimensiones)
    {
        await host.StartAsync();
        var cancellationToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        await host.Services.GetRequiredService<IDimensionLoader>().CargarAsync(cancellationToken);
        await host.StopAsync();
    }
    else
    {
        await host.RunAsync();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "El Worker Service se detuvo por un error no controlado.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
