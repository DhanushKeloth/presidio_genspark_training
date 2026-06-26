// Controllers/UsersController.cs
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApi.Data;
using UserManagementApi.Models;

namespace UserManagementApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserDbContext _context;

        public UsersController(UserDbContext context)
        {
            _context = context;
        }

        // 1. POST: api/users (Create a single user)
        [HttpPost]
        public async Task<IActionResult> CreateUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "User created successfully", UserId = user.Id });
        }

        // 2. POST: api/users/upload (Bulk upload via Excel)
        [HttpPost("upload")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Please upload a valid Excel file.");

            if (!file.FileName.EndsWith(".xlsx"))
                return BadRequest("Only .xlsx files are supported.");

            var users = new List<User>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    // Assuming the data is in the first worksheet
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip the header row

                    foreach (var row in rows)
                    {
                        var user = new User
                        {
                            Name = row.Cell(1).GetValue<string>(),
                            Email = row.Cell(2).GetValue<string>(),
                            PhoneNumber = row.Cell(3).GetValue<string>(),
                            Age = row.Cell(4).GetValue<int>()
                        };
                        users.Add(user);
                    }
                }
            }

            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"{users.Count} users uploaded successfully." });
        }

        // 3. GET: api/users/download (Download all users as Excel)
        [HttpGet("download")]
        public async Task<IActionResult> DownloadExcel()
        {
            var users = await _context.Users.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Users");

                // Add Headers
                worksheet.Cell(1, 1).Value = "Id";
                worksheet.Cell(1, 2).Value = "Name";
                worksheet.Cell(1, 3).Value = "Email";
                worksheet.Cell(1, 4).Value = "PhoneNumber";
                worksheet.Cell(1, 5).Value = "Age";

                // Add Data
                for (int i = 0; i < users.Count; i++)
                {
                    worksheet.Cell(i + 2, 1).Value = users[i].Id;
                    worksheet.Cell(i + 2, 2).Value = users[i].Name;
                    worksheet.Cell(i + 2, 3).Value = users[i].Email;
                    worksheet.Cell(i + 2, 4).Value = users[i].PhoneNumber;
                    worksheet.Cell(i + 2, 5).Value = users[i].Age;
                }

                // Format the header row
                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();

                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UsersList.xlsx");
                }
            }
        }
    }
}