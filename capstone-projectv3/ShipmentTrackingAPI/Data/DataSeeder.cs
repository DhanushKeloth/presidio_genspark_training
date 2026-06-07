// using Microsoft.AspNetCore.Identity;
// using Microsoft.EntityFrameworkCore;
// using ShipmentTrackingAPI.Models;
// using ShipmentTrackingAPI.Models.Enums;

// namespace ShipmentTrackingAPI.Data;

// public static class DataSeeder
// {
//     public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
//     {
//         using var scope = serviceProvider.CreateScope();
//         var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//         var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

//         // Check if the admin already exists
//         if (!await context.Users.AnyAsync(u => u.Email == "admin@swiftparcel.com"))
//         {
//             var adminUser = new User
//             {
//                 Email = "admin@swiftparcel.com",
//                 FullName = "Platform Admin",
//                 Role = UserRole.Admin,
//                 IsActive = true,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };

//             // Hash the password "Admin@123!" (Change this to whatever you want)
//             adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@123!");

//             context.Users.Add(adminUser);
//             await context.SaveChangesAsync();
//         }
//     }
// }