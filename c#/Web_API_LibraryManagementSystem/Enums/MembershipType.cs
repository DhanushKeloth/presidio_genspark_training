
namespace LibraryManagementSystem.Enums
{
    public enum MembershipType
    {
        Basic = 1,
        Student = 2,
        Premium = 3
    }
}

// dotnet ef dbcontext scaffold "Host=localhost;Database=library_management;Username=dhanushkeloth;Password=1234" Npgsql.EntityFrameworkCore.PostgreSQL --output-dir Models --context-dir Data --context LibraryDbContext --force