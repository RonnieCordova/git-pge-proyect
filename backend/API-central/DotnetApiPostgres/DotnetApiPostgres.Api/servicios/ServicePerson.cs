
using DotnetApiPostgres.Api;
using DotnetApiPostgres.Api.Modelos;
using DotnetApiPostgres.Api.Modelos.DTO;
using Microsoft.EntityFrameworkCore;

public interface IpPersonService
{
    Task<GetPersonDto> AddPersonAsync(CreatePersonDTO personToCreate);
    Task UpdatePersonAsync(UpdatePersonDTO personToUpdate);
    Task DeletePersonAsync(Persona persona);
    Task<GetPersonDto?> FindPersonByIdAsync(int id);
    Task<IEnumerable<GetPersonDto>> GetPeopleAsync();
}

public sealed class PersonService : IpPersonService
{
    private readonly ApplicationDbContext _context;

    public PersonService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetPersonDto> AddPersonAsync(CreatePersonDTO personToCreate)
    {
        Persona persona = CreatePersonDTO.ToPerson(personToCreate);
        _context.personas.Add(persona);
        await _context.SaveChangesAsync();
        return Persona.ToGetPersonDto(persona);
    }
    public async Task UpdatePersonAsync(UpdatePersonDTO personToUpdate)
    {
        Persona persona = UpdatePersonDTO.ToPerson(personToUpdate);
        _context.personas.Update(persona);
        await _context.SaveChangesAsync();
    }

    public async Task DeletePersonAsync(Persona persona)
    {
        _context.personas.Remove(persona);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<GetPersonDto>> GetPeopleAsync()
    {
        IEnumerable<Persona> personas = await _context.personas.AsNoTracking().ToListAsync();
        return personas.Select(Persona.ToGetPersonDto);
    }

    public async Task<GetPersonDto?> FindPersonByIdAsync(int id)
    {
        Persona? persona = await _context.personas.Where(x => x.Id == id).AsNoTracking().FirstOrDefaultAsync();
        if (persona == null)
        {
            return null;
        }
        return Persona.ToGetPersonDto(persona);
    }

   
}