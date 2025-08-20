using DotnetApiPostgres.Api.Modelos.DTO;
using Microsoft.AspNetCore.Mvc;

namespace DotnetApiPostgres.Api.Controllers;

[Route("api/personas")]
[ApiController]
public class PeopleController : ControllerBase
{
    private readonly IpPersonService _personService;
    private readonly ILogger<PeopleController> _logger;

    public PeopleController(IpPersonService personService, ILogger<PeopleController> logger)
    {
        _personService = personService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> AddPersonAsync(CreatePersonDTO personToCreate)
    {
        try
        {
            var person = await _personService.AddPersonAsync(personToCreate);
            return Ok(person);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePersonAsync(int id, UpdatePersonDTO personToUpdate)
    {
        if (id != personToUpdate.Id)
        {
            return BadRequest($"id in parameter and id in body is different. id in parameter: {id}, id in body: {personToUpdate.Id}");
        }
        try
        {
            GetPersonDto? person = await _personService.FindPersonByIdAsync(id);
            if (person == null)
            {
                return NotFound();
            }
            await _personService.UpdatePersonAsync(personToUpdate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
    [HttpGet]
    public async Task<IActionResult> GetPeopleAsync()
    {
        try
        {
            IEnumerable<GetPersonDto> peoples = await _personService.GetPeopleAsync();
            return Ok(peoples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
