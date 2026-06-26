
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Models;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<Book> Books {get;set;}=null!;
    
}