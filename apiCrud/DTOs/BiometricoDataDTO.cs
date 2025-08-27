using System.ComponentModel.DataAnnotations;

namespace ef_core.DTOs
{
    public class BiometricoDataDTO
    {
        [Key]
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public DateTime? Hora { get; set; }
        public string? Detalle { get; set; }
        public bool EsEntrada { get; set; }
        public bool EsSalida { get; set; }
        public bool EsSalidaAlmuerzo { get; set; }
        public bool EsLlegadaAlmuerzo { get; set; }
    }
}