using System.ComponentModel.DataAnnotations;

namespace DotnetApiPostgres.Api.Modelos.DTO;

public class UpdatePersonDTO
{
    
    public required string Id { get; set; }

    [Required]
    public required string nombre { get; set; }
    public static Persona ToPerson(UpdatePersonDTO updatePersonDTO)
    {
        return new Persona
        {
            Id = int.Parse(updatePersonDTO.Id),
            nombre = updatePersonDTO.nombre
        };
    }

}