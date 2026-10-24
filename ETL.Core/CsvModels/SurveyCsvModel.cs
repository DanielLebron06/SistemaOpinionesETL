

namespace ETL.Core.CsvModels
{
    public class SurveyCsvModel
    {
        public string IdOpinion { get; set; }
        public string IdCliente { get; set; }
        public string IdProducto { get; set; }
        public DateTime Fecha { get; set; }
        public string Comentario { get; set; }
        public string Clasificación { get; set; }
        public int PuntajeSatisfacción { get; set; }
        public string Fuente { get; set; }
    }
}
