using BusBookingAPI.Data;
using BusBookingAPI.Helpers;
using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Models.Entities;
using BusBookingAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusBookingAPI.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly BusBookingDbContext _db;
    private readonly JwtHelper _jwt;

    public AuthService(BusBookingDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    // ---- User Registration ----
    public async Task<(bool Success, string Message, int StatusCode)> RegisterUserAsync(UserRegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return (false, "Passwords do not match", 400);

        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            return (false, "Email already registered", 409);

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Status = "active"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, "Registration successful", 201);
    }

    // ---- User Login ----
    public async Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginUserAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return (false, null, "Invalid credentials", 401);

        if (user.Status != "active")
            return (false, null, "Account suspended", 403);

        var token = _jwt.GenerateToken(user.Id, user.Email, "User");
        return (true, new LoginResponse
        {
            Token = token,
            ExpiresAt = _jwt.GetExpiry(),
            Role = "User",
            Email = user.Email
        }, "Login successful", 200);
    }

    // ---- Operator Registration ----
    public async Task<(bool Success, string Message, int StatusCode)> RegisterOperatorAsync(OperatorRegisterRequest request)
    {
        var exists = await _db.Operators.AnyAsync(o => o.Email == request.Email);
        if (exists)
            return (false, "Email already registered", 409);

        var op = new Operator
        {
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
            GstNumber = request.GstNumber,
            Address = request.Address,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Status = "pending"
        };

        _db.Operators.Add(op);
        await _db.SaveChangesAsync();

        return (true, "Registration submitted. Awaiting admin approval.", 201);
    }

    // ---- Operator Login ----
    public async Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginOperatorAsync(LoginRequest request)
    {
        var op = await _db.Operators.FirstOrDefaultAsync(o => o.Email == request.Email);
        if (op == null || !BCrypt.Net.BCrypt.Verify(request.Password, op.PasswordHash))
            return (false, null, "Invalid credentials", 401);

        if (op.Status != "approved")
            return (false, null, "Account pending approval or rejected/disabled", 403);

        var token = _jwt.GenerateToken(op.Id, op.Email, "Operator");
        return (true, new LoginResponse
        {
            Token = token,
            ExpiresAt = _jwt.GetExpiry(),
            Role = "Operator",
            Email = op.Email
        }, "Login successful", 200);
    }

    // ---- Admin Login ----
    public async Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginAdminAsync(LoginRequest request)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (admin == null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            return (false, null, "Invalid credentials", 401);

        if (admin.Role != "admin" && admin.Role != "super_admin")
            return (false, null, "Insufficient privileges", 403);

        var token = _jwt.GenerateToken(admin.Id, admin.Email, "Admin");
        return (true, new LoginResponse
        {
            Token = token,
            ExpiresAt = _jwt.GetExpiry(),
            Role = "Admin",
            Email = admin.Email
        }, "Login successful", 200);
    }
}
