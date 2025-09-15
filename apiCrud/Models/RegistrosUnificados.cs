using System;
using System.Collections.Generic;

namespace ef_core.Data // O el namespace que corresponda a tu proyecto
{
    /// <summary>
    /// Representa un único registro diario de asistencia para un empleado,
    /// consolidado a partir de las fuentes de SeatData y BiometricoData.
    /// Este objeto es el resultado final que se enviará a la interfaz web.
    /// </summary>
    public class RegistroConsolidado
    {
        public int IdEmpleado { get; set; }
        public string? Area { get; set; }
        public string? NombreCompleto { get; set; }
        public DateOnly Fecha { get; set; }

        public DateTime? HoraEntrada { get; set; }
        public DateTime? HoraSalidaAlmuerzo { get; set; }
        public DateTime? HoraRegresoAlmuerzo { get; set; }
        public DateTime? HoraSalida { get; set; }

        /// <summary>
        /// Describe el estado del registro.
        /// Ejemplos: "Completo", "Falta Salida", "Sin registro de Almuerzo".
        /// </summary>
        public string? Estado { get; set; }

        /// <summary>
        /// Lista que detalla la fuente de cada marcación para auditoría.
        /// Ejemplo: ["Entrada: SEAT", "Salida: Biometrico"].
        /// </summary>
        public List<string> Fuentes { get; set; }
        public string? TipoPermiso { get; set; }

        public RegistroConsolidado()
        {
            // Inicializamos la lista para evitar errores de referencia nula.
            Fuentes = new List<string>();
        }
    }
}