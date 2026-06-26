using Auth0.AspNetCore.Authentication.Api;
using Auth0.Controllers;
using Microsoft.AspNetCore.Builder;

using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. Register Auth0 API Authentication
builder.Services.AddAuth0ApiAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"]??"";
    options.Audience = builder.Configuration["Auth0:Audience"]??""; 
});


builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();

// 2. Enable Authentication & Authorization Middleware
// IMPORTANT: UseAuthentication must always come BEFORE UseAuthorization
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();