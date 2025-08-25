using System.ComponentModel.DataAnnotations;

namespace ef_core.DTOs
{
    public class RawEventDTO
    {
        public int UsuarioId { get; set; }
        public string? DispositivoId { get; set; } 
        public string? TipoEvento { get; set; }
        public DateTime MarcaDeTiempo { get; set; }
        public string? Payload_json { get; set; }
        public string? Lote_ingesta { get; set; }
    }
}