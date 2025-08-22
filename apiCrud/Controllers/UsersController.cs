
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ef_core.Data;

[Route("api/[controller]")]
[ApiController]

public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Created();
    }
}