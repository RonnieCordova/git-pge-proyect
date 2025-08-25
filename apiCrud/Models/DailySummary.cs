namespace ef_core.Data;

//Creacion de la clase de resumen diario
public class DailySummary
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public required DateTime Fecha { get; set; }
    public TimeSpan? HoraEntrada { get; set; } 
    public TimeSpan? HoraSalidaAlmuerzo { get; set; } 
    public TimeSpan? HoraRegresoAlmuerzo { get; set; }
    public TimeSpan? HoraSalida { get; set; }
    public required string FuenteEntrada { get; set; }
    public required string FuenteSalida { get; set; }
    public required string Estado { get; set; }
    public int MinutoTarde { get; set; }
    public string? Notas { get; set; }
}