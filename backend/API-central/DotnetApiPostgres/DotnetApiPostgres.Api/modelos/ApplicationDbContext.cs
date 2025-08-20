using DotnetApiPostgres.Api.Modelos;
using Microsoft.EntityFrameworkCore;

namespace DotnetApiPostgres.Api;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Persona> personas { get; set; }
}