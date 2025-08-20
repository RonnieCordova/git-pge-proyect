using System.ComponentModel.DataAnnotations;

namespace DotnetApiPostgres.Api.Modelos.DTO;

public class CreatePersonDTO
{
    [Required]
    [StringLength(100)]
    public required string nombre { get; set; }

    public static Persona ToPerson(CreatePersonDTO dto)
    {
        return new Persona
        {
            nombre = dto.nombre
        };
    }

}