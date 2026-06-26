using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.NameTranslation;
using ShipmentTrackingAPI.BackgroundServices;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Hubs;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Middleware;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.DTOs.Common;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services;
using ShipmentTrackingAPI.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200","http://127.0.0.1:5500") // Your Angular URL
              .AllowAnyHeader()                     // Allows the Authorization Bearer token!
              .AllowAnyMethod()
              .AllowCredentials();                // Allows POST, GET, PUT, DELETE
    });
});

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


#endregion

#region Dependency Injection (Repo & services)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();

builder.Services.AddScoped<IShipmentService, ShipmentService>();

builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddSingleton<ITrackingService, TrackingService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddSingleton<IGpsSimulationChannel, GpsSimulationChannel>();
builder.Services.AddHostedService<GpsSimulationService>();

// Register Pricing Settings for IOptions injection
builder.Services.Configure<PricingSettings>(builder.Configuration.GetSection("PricingSettings"));
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
    options.Events = new JwtBearerEvents
    {
        // Existing: extract token from query-string for SignalR connections
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            Console.WriteLine($"[SignalR Auth] Path={path}, TokenPresent={!string.IsNullOrEmpty(accessToken)}");

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/tracking"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },

        // NEW: Return JSON 401 instead of HTML challenge when token is missing/invalid
        OnChallenge = async context =>
        {
            context.HandleResponse(); // suppress the default HTML response
            context.Response.StatusCode  = 401;
            context.Response.ContentType = "application/json";
            var body = JsonSerializer.Serialize(new
            {
                statusCode = 401,
                message    = "Authentication required. Please provide a valid Bearer token."
            });
            await context.Response.WriteAsync(body);
        },

        // NEW: Return JSON 403 instead of HTML page when role/policy check fails
        OnForbidden = async context =>
        {
            context.Response.StatusCode  = 403;
            context.Response.ContentType = "application/json";
            var body = JsonSerializer.Serialize(new
            {
                statusCode = 403,
                message    = "You do not have permission to access this resource."
            });
            await context.Response.WriteAsync(body);
        }
    };
});

builder.Services.AddAuthorization();
#endregion

builder.Services.AddSignalR();

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // This tells the API to return "PendingApproval" instead of 0
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();
#region Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
#endregion


// try
// {
//     await DataSeeder.SeedAdminAsync(app.Services);
// }
// catch (Exception ex)
// {
//     // If the database isn't ready or migrations haven't run, it logs the error
//     var logger = app.Services.GetRequiredService<ILogger<Program>>();
//     logger.LogError(ex, "An error occurred while seeding the admin user.");
// }

// app.Run();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");
app.Run();
