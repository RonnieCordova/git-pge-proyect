using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DailySummary
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateTime Fecha { get; set; }

    // Información del empleado
    public int EmpleadoId { get; set; }
    public string? NombreCompleto { get; set; }

    // Horarios finales consolidados y calculados
    public TimeSpan? HoraEntrada { get; set; }
    public TimeSpan? HoraSalidaAlmuerzo { get; set; }
    public TimeSpan? HoraRegresoAlmuerzo { get; set; }
    public TimeSpan? HoraSalida { get; set; }

    // Métricas de tiempo
    public TimeSpan? HorasTrabajadas { get; set; }
    public TimeSpan? HorasAlmuerzo { get; set; }

    // Estado del día
    public string? EstadoDelDia { get; set; } // Ejemplo: "Completo", "Inconsistente", "Ausente"
    public string? Observaciones { get; set; }

    // Propiedad de navegación a RawEvents
    public List<RawEvent>? EventosRelacionados { get; set; }
}