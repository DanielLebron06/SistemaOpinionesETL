using ETL.Application;
using ETL.Application.Options;
using ETL.Infrastructure;
using ETL.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Logging centralizado: ILogger<T> en todo el código, Serilog como proveedor (consola + archivo).
builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddOptions<EtlOptions>()
    .Bind(builder.Configuration.GetSection(EtlOptions.Seccion))
    .ValidateOnStart();

// Clean Architecture: cada capa registra sus propios servicios.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ExtractionWorker>();

var host = builder.Build();

try
{
    await host.RunAsync();
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
