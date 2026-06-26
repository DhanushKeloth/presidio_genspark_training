
using Microsoft.EntityFrameworkCore;

public class AppDbContext: DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=tododb;Username=dhanushkeloth;Password=1234");
    }
    public DbSet<TodoItem> Todos {get;set;}
}