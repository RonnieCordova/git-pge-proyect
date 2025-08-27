using Microsoft.EntityFrameworkCore;
namespace ef_core.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<BiometricoData> BiometricoData { get; set; }
    public DbSet<SeatData> SeatData { get; set; }
}