using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.NameTranslation;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Middleware;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services;

var builder = WebApplication.CreateBuilder(args);

#region enums configuration
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
var translator = new NpgsqlNullNameTranslator();

dataSourceBuilder.MapEnum<UserRole>("user_role", nameTranslator: translator);
dataSourceBuilder.MapEnum<DriverAccountStatus>("driver_account_status", nameTranslator: translator);
dataSourceBuilder.MapEnum<DriverOpStatus>("driver_op_status", nameTranslator: translator);
dataSourceBuilder.MapEnum<ShipmentStatus>("shipment_status", nameTranslator: translator);
dataSourceBuilder.MapEnum<AddressType>("address_type", nameTranslator: translator);
dataSourceBuilder.MapEnum<OtpType>("otp_type", nameTranslator: translator);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions => 
    {
        npgsqlOptions.MapEnum<UserRole>("user_role", nameTranslator: translator);
        npgsqlOptions.MapEnum<DriverAccountStatus>("driver_account_status", nameTranslator: translator);
        npgsqlOptions.MapEnum<DriverOpStatus>("driver_op_status", nameTranslator: translator);
        npgsqlOptions.MapEnum<ShipmentStatus>("shipment_status", nameTranslator: translator);
        npgsqlOptions.MapEnum<AddressType>("address_type", nameTranslator: translator);
        npgsqlOptions.MapEnum<OtpType>("otp_type", nameTranslator: translator);
    }));


// 2. Register the DbContext using the configured data source
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));
#endregion

#region Dependency Injection (Repo & services)
builder.Services.AddScoped<IUserRepository,UserRepository>();
builder.Services.AddScoped<IDriverRepository,DriverRepository>();

builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService,AuthService>();
#endregion

#region 3. Authentication & Authorization (JWT)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

builder.Services.AddAuthorization();
#endregion


builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
#region Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
#endregion
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
