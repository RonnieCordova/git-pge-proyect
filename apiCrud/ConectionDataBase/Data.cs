using Microsoft.EntityFrameworkCore;
namespace ef_core.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<RawEvent> RawEvents { get; set; }
}