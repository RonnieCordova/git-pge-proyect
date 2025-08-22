using System.ComponentModel.DataAnnotations;
using ef_core.Data;

namespace ef_core.Data;

//Creacion de la entidad de usuarios
public class User
{
    public int Id { get; set; }
    public required string nombre { get; set; }
    public required string apellido { get; set; }

}

//Creacion de la entidad de eventos crudos
/*public class RawEvents
{
    public int Id { get; set; }
    public int user_id { get; set; }
    public int device_id { get; set; }
    public required string event_type { get; set; }
    public required string timestamp { get; set; }
    public string payload_json { get; set; }
    public string lote_ingesta { get; set; }

}*/