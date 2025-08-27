using System.ComponentModel.DataAnnotations;

namespace ef_core.DTOs
{
    public class SeatDataDTO
    {
        [Key]
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public DateTime? HoraEntrada { get; set; } 
        public DateTime? HoraSalidaAlmuerzo { get; set; } 
        public DateTime? HoraRegresoAlmuerzo { get; set; }
        public DateTime? HoraSalida { get; set; }
        public string? Detalle { get; set; }
    }
}