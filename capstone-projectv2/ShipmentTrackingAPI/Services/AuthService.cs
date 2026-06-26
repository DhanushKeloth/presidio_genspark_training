using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ShipmentTrackingAPI.DTOs.Auth;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Exceptions;

namespace ShipmentTrackingAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly IConfiguration _config;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthService(
            IUserRepository userRepo, 
            IDriverRepository driverRepo,
            IConfiguration config, 
            IPasswordHasher<User> passwordHasher)
        {
            _userRepo = userRepo;
            _driverRepo = driverRepo;
            _config = config;
            _passwordHasher = passwordHasher;
        }

        public async Task<RegisterResponseDto> RegisterCustomerAsync(RegisterCustomerDto request)
        {
            if (await _userRepo.EmailExistsAsync(request.Email))
                throw new ConflictException("Email is already registered.");

            var user = new User
            {
                Email = request.Email.Trim(),
                FullName = request.FullName.Trim(),
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            // Attach the 1:1 Customer Profile and map the optional fields
            user.CustomerProfile = new CustomerProfile
            {
                PhoneNumber = request.PhoneNumber?.Trim(),
                AlternatePhoneNumber = request.AlternatePhoneNumber?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveAsync();

            return new RegisterResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                Message = "Customer registration successful."
            };
        }

        public async Task<RegisterResponseDto> RegisterDriverAsync(RegisterDriverDto request)
        {
            // Upgraded to ConflictException
            if (await _userRepo.EmailExistsAsync(request.Email))
                throw new ConflictException("Email is already registered.");

            if (await _driverRepo.LicenseNumberExistsAsync(request.LicenseNumber))
                throw new ConflictException("License number is already registered.");

            var user = new User
            {
                Email = request.Email.Trim(),
                FullName = request.FullName.Trim(),
                Role = UserRole.Driver,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            // Attach the 1:1 Driver Profile with sanitized data
            user.DriverProfileUser = new DriverProfile
            {
                PhoneNumber = request.PhoneNumber.Trim(),
                VehicleType = request.VehicleType.Trim(),
                VehicleNumber = request.VehicleNumber.Trim(),
                LicenseNumber = request.LicenseNumber.Trim(),
                AccountStatus = DriverAccountStatus.PendingApproval, 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveAsync();

            return new RegisterResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                Message = "Driver registration successful. Account is pending admin approval."
            };
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            // Upgraded all auth failures to custom Exceptions
            var user = await _userRepo.GetByEmailAsync(request.Email.Trim()) 
                ?? throw new UnauthorizedException("Invalid email or password.");

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
                throw new UnauthorizedException("Invalid email or password.");

            // ==========================================
            // STRICT ACCOUNT STATUS CHECKS
            // ==========================================
            if (user.Role == UserRole.Driver)
            {
                var driverStatus = user.DriverProfileUser?.AccountStatus;

                if (driverStatus == DriverAccountStatus.PendingApproval)
                    throw new ForbiddenException("Your driver account is pending admin approval. You will be notified once verified.");

                if (driverStatus == DriverAccountStatus.Suspended)
                    throw new ForbiddenException("Your driver account has been suspended. Please contact dispatch support.");

                if (driverStatus == DriverAccountStatus.Deleted)
                    throw new ForbiddenException("This driver profile has been removed.");
            }
            else 
            {
                // Customers and Admins fallback
                if (!user.IsActive)
                    throw new ForbiddenException("This account has been deactivated.");
            }

            return GenerateLoginResponse(user);
        }

        private LoginResponseDto GenerateLoginResponse(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var expirationHours = 8;
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: credentials);

            return new LoginResponseDto
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresIn = expirationHours * 3600, 
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role.ToString()
            };
        }
    }
}