using ETL.Core.Entities;
using ETL.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


class Program
{
    static async Task Main(string[] args)
    {
        // 1. Configurar la Inyección de Dependencias
        var services = new ServiceCollection();

        // Cadena de conexión a tu base de datos local
        string connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=SistemaOpinionesClientes;Trusted_Connection=True;";

        services.AddDbContext<SistemaOpinionesClientesContext>(options =>
            options.UseSqlServer(connectionString));

        // Registrar servicios individuales y el maestro
        services.AddScoped<FuenteDatoService>();
        services.AddScoped<ClienteService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<OpinionService>();
        services.AddScoped<MasterETLService>();

        var serviceProvider = services.BuildServiceProvider();

        // 2. Obtener el servicio maestro y ejecutar el ETL
        var etlMaster = serviceProvider.GetRequiredService<MasterETLService>();

        // Reemplaza esta ruta con la carpeta real donde tienes tus archivos CSV descargados
        string rutaDirectorioCsvs = @"C:\Users\danie\Desktop\uni\Big data\Proyecto Sistema Opiniones\Datos originales";

        if (!Directory.Exists(rutaDirectorioCsvs))
        {
            Console.WriteLine($"[ERROR] La ruta no existe: {rutaDirectorioCsvs}");
            return;
        }

        Console.WriteLine("Archivos encontrados en la carpeta:");
        foreach (var file in Directory.GetFiles(rutaDirectorioCsvs, "*.csv"))
        {
            Console.WriteLine($" - {Path.GetFileName(file)}");
        }

        await etlMaster.EjecutarProcesoETLAsync(rutaDirectorioCsvs);

        Console.WriteLine("\nProceso finalizado. Presiona cualquier tecla para salir.");
        Console.ReadKey();
    }
}