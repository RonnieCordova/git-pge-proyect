using System.ComponentModel.DataAnnotations;

namespace DotnetApiPostgres.Api.Modelos.DTO;

public class GetPersonDto
{
    public int Id { get; set; }
    public required string? nombre { get; set; }

    public static GetPersonDto ToGetPersonDto(GetPersonDto dto)
    {
        return new GetPersonDto
        {
            Id = dto.Id,
            nombre = dto.nombre
        };
    }
}
