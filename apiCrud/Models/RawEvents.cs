using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ef_core.Data;

//Creacion de la clase de eventos crudos
public class RawEvent
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string? DispositivoId { get; set; }
    public string? TipoEvento { get; set; }
    public DateTime MarcaDeTiempo { get; set; }
    public string? Payload_json { get; set; }
    public string? Lote_ingesta { get; set; }

}