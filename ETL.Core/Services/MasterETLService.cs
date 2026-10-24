using ETL.Core.Entities; // Asegúrate de incluir el namespace correcto de tus entidades
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ETL.Data.Services
{
    public class MasterETLService
    {
        private readonly SistemaOpinionesClientesContext _context;
        private readonly FuenteDatoService _fuenteDatoService;
        private readonly ClienteService _clienteService;
        private readonly ProductoService _productoService;
        private readonly OpinionService _opinionService;

        public MasterETLService(
            SistemaOpinionesClientesContext context,
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
            await AsegurarTiposFeedbackAsync();

            // Leer los IDs reales asignados por la BD (IDENTITY puede no empezar en 1)
            var tiposFeedback = await _context.TiposFeedbacks.AsNoTracking().ToListAsync();
            var idPositivo = tiposFeedback.FirstOrDefault(t => t.NombreTipo == "Positivo")?.IdTipoFeedback
                ?? throw new InvalidOperationException("No se encontró TipoFeedback 'Positivo' en la BD.");
            var idNeutral = tiposFeedback.FirstOrDefault(t => t.NombreTipo == "Neutral")?.IdTipoFeedback
                ?? throw new InvalidOperationException("No se encontró TipoFeedback 'Neutral' en la BD.");
            var idNegativo = tiposFeedback.FirstOrDefault(t => t.NombreTipo == "Negativo")?.IdTipoFeedback
                ?? throw new InvalidOperationException("No se encontró TipoFeedback 'Negativo' en la BD.");

            Console.WriteLine($"-> TiposFeedback: Positivo={idPositivo}, Neutral={idNeutral}, Negativo={idNegativo}");

            // Resolver un IdFuente representativo por TipoFuente
            var idFuenteCSV      = await ResolverIdFuentePorTipoAsync("CSV");
            var idFuenteRedSocial = await ResolverIdFuentePorTipoAsync("Red Social");
            var idFuenteWeb      = await ResolverIdFuentePorTipoAsync("Web");

            Console.WriteLine($"-> Fuentes: CSV={idFuenteCSV}, Red Social={idFuenteRedSocial}, Web={idFuenteWeb}");

            // 4. Cargar tablas dependientes (Hijas - Opiniones)
            Console.WriteLine("\n[4.1/5] Cargando Encuestas...");
            await _opinionService.CargarEncuestasAsync(
                Path.Combine(directorioCsvs, "surveys_part1.csv"),
                idFuenteCSV, idPositivo, idNeutral, idNegativo);

            // Asegurar cliente anónimo para comentarios sociales sin IdCliente
            await AsegurarClienteAnonimoAsync();

            Console.WriteLine("\n[4.2/5] Cargando Comentarios Sociales...");
            await _opinionService.CargarComentariosSocialesAsync(
                Path.Combine(directorioCsvs, "social_comments.csv"),
                idFuenteRedSocial, idNeutral);

            Console.WriteLine("\n[4.3/5] Cargando Reseñas Web...");
            await _opinionService.CargarResenasWebAsync(
                Path.Combine(directorioCsvs, "web_reviews.csv"),
                idFuenteWeb, idPositivo, idNeutral, idNegativo);

            Console.WriteLine("\n========================================");
            Console.WriteLine("    PROCESO ETL FINALIZADO CON ÉXITO    ");
            Console.WriteLine("========================================");
        }

        /// <summary>
        /// Devuelve el primer IdFuente que tenga el TipoFuente indicado.
        /// Lanza excepción si no existe ninguno en la BD.
        /// </summary>
        private async Task<string> ResolverIdFuentePorTipoAsync(string tipoFuente)
        {
            var id = await _context.FuentesDatos
                .AsNoTracking()
                .Where(f => f.TipoFuente == tipoFuente)
                .OrderBy(f => f.IdFuente)
                .Select(f => f.IdFuente)
                .FirstOrDefaultAsync();

            return id ?? throw new InvalidOperationException(
                $"No existe ninguna FuenteDatos con TipoFuente='{tipoFuente}' en la BD.");
        }

        /// <summary>
        /// Inserta Positivo/Neutral/Negativo si la tabla está vacía,
        /// o añade los que falten si ya existen otros registros.
        /// </summary>
        private async Task AsegurarTiposFeedbackAsync()
        {
            var nombresExistentes = await _context.TiposFeedbacks
                .AsNoTracking()
                .Select(t => t.NombreTipo)
                .ToListAsync();

            var requeridos = new[] { "Positivo", "Neutral", "Negativo" };
            var faltantes = requeridos.Except(nombresExistentes).ToList();

            if (faltantes.Count == 0)
            {
                Console.WriteLine("-> Tipos de Feedback ya existentes, se reusan.");
                return;
            }

            foreach (var nombre in faltantes)
                _context.TiposFeedbacks.Add(new TiposFeedback { NombreTipo = nombre });

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            Console.WriteLine($"-> {faltantes.Count} tipo(s) de Feedback insertado(s): {string.Join(", ", faltantes)}");
        }

        /// <summary>
        /// Garantiza que exista un cliente centinela "ANONIMO" en la BD
        /// para absorber comentarios sociales sin IdCliente identificado.
        /// </summary>
        private async Task AsegurarClienteAnonimoAsync()
        {
            const string idAnonimo = "ANONIMO";
            var existe = await _context.Clientes
                .AsNoTracking()
                .AnyAsync(c => c.IdCliente == idAnonimo);

            if (existe)
            {
                Console.WriteLine("-> Cliente anónimo ya existe, se reusa.");
                return;
            }

            _context.Clientes.Add(new Cliente
            {
                IdCliente = idAnonimo,
                Nombre = "Cliente Anónimo",
                Email = "anonimo@sistema.com"
            });

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            Console.WriteLine("-> Cliente anónimo insertado correctamente.");
        }
    }
}
