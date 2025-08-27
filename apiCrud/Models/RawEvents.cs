using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class RawEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // Propiedades para unificar los datos de ambos sistemas
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public DateTime FechaEvento { get; set; }

    // Propiedades de la data del Biometrico
    public bool? EsEntradaBiometrico { get; set; }
    public bool? EsSalidaBiometrico { get; set; }
    public bool? EsSalidaAlmuerzoBiometrico { get; set; }
    public bool? EsLlegadaAlmuerzoBiometrico { get; set; }
    
    // Propiedades de la data del Seath (TimeSpan)
    public TimeSpan? HoraEntradaSeath { get; set; }
    public TimeSpan? HoraSalidaAlmuerzoSeath { get; set; }
    public TimeSpan? HoraRegresoAlmuerzoSeath { get; set; }
    public TimeSpan? HoraSalidaSeath { get; set; }

    // Propiedad para el origen del dato (Biometrico, Seath, etc.)
    public string? Origen { get; set; }
    public string? DetallesAdicionales { get; set; }
}