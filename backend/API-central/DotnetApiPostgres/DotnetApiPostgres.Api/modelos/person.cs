using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DotnetApiPostgres.Api.Modelos.DTO;

namespace DotnetApiPostgres.Api.Modelos;

[Table("Persona")]
public class Persona
{
    public int Id { get; set; }

    [Column(TypeName = "varchar(30)")]
    [Required]
    public required string nombre { get; set; }

    public static GetPersonDto ToGetPersonDto(Persona persona)
    {
        return new GetPersonDto
        {
            Id = persona.Id,
            nombre = persona.nombre
        };
    }

}
