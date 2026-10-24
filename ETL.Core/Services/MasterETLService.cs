using ETL.Core.Entities; // Asegúrate de incluir el namespace correcto de tus entidades
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ETL.Data.Services
{
    public class MasterETLService
    {
        private readonly SistemaOpinionesClientesContext _context; // 1. Agregamos el contexto
        private readonly FuenteDatoService _fuenteDatoService;
        private readonly ClienteService _clienteService;
        private readonly ProductoService _productoService;
        private readonly OpinionService _opinionService;

        public MasterETLService(
            SistemaOpinionesClientesContext context, // 2. Lo inyectamos en el constructor
            FuenteDatoService fuenteDatoService,
            ClienteService clienteService,
            ProductoService productoService,
            OpinionService opinionService)
        {
            _context = context;
            _fuenteDatoService = fuenteDatoService;
            _clienteService = clienteService;
            _productoService = productoService;
            _opinionService = opinionService;
        }

        public async Task EjecutarProcesoETLAsync(string directorioCsvs)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    INICIANDO PROCESO ETL AUTOMÁTICO    ");
            Console.WriteLine("========================================");

            // 1. Cargar tablas independientes (Padres)
            Console.WriteLine("\n[1/5] Cargando Fuentes de Datos...");
            await _fuenteDatoService.CargarFuentesAsync(Path.Combine(directorioCsvs, "fuente_datos.csv"));

            Console.WriteLine("\n[2/5] Cargando Clientes...");
            await _clienteService.CargarClientesAsync(Path.Combine(directorioCsvs, "clients.csv"));

            Console.WriteLine("\n[3/5] Cargando Productos...");
            await _productoService.CargarProductosAsync(Path.Combine(directorioCsvs, "products.csv"));

            // 3.5 ASEGURAR TIPOS DE FEEDBACK ANTES DE INSERTAR OPINIONES
            Console.WriteLine("\n[3.5/5] Verificando Tipos de Feedback...");
            if (!await _context.TiposFeedbacks.AnyAsync())
            {
                _context.TiposFeedbacks.AddRange(
                    new TiposFeedback { NombreTipo = "Positivo" },
                    new TiposFeedback { NombreTipo = "Neutral" },
                    new TiposFeedback { NombreTipo = "Negativo" }
                );
                await _context.SaveChangesAsync();
                Console.WriteLine("-> Tipos de Feedback insertados correctamente.");
            }

            // 2. Cargar tablas dependientes (Hijas - Opiniones)
            string idFuenteDefault = "F001";

            Console.WriteLine("\n[4.1/5] Cargando Encuestas...");
            await _opinionService.CargarEncuestasAsync(Path.Combine(directorioCsvs, "surveys_part1.csv"), idFuenteDefault);

            Console.WriteLine("\n[4.2/5] Cargando Comentarios Sociales...");
            await _opinionService.CargarComentariosSocialesAsync(Path.Combine(directorioCsvs, "social_comments.csv"), idFuenteDefault);

            Console.WriteLine("\n[4.3/5] Cargando Reseñas Web...");
            await _opinionService.CargarResenasWebAsync(Path.Combine(directorioCsvs, "web_reviews.csv"), idFuenteDefault);

            Console.WriteLine("\n========================================");
            Console.WriteLine("    PROCESO ETL FINALIZADO CON ÉXITO    ");
            Console.WriteLine("========================================");
        }
    }
}