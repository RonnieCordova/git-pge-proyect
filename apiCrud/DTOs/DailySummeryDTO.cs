namespace ef_core.DTOs
{
    public class DailySummaryDTO
    {
        public int UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan? HoraEntrada { get; set; }
        public TimeSpan? HoraSalidaAlmuerzo { get; set; }
        public TimeSpan? HoraRegresoAlmuerzo { get; set; }
        public TimeSpan? HoraSalida { get; set; }
        public string? FuenteEntrada { get; set; }
        public string? FuenteSalida { get; set; }
        public string? Estado { get; set; }
        public int MinutosTarde { get; set; }
        public string? Notas { get; set; }
    }
}