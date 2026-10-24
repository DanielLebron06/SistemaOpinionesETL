

namespace ETL.Core.CsvModels
{
    public class WebReviewCsvModel
    {
        public string IdReview { get; set; }
        public string IdCliente { get; set; }
        public string IdProducto { get; set; }
        public DateTime Fecha { get; set; }
        public string Comentario { get; set; }
        public int Rating { get; set; }
    }
}
