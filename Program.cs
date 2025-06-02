using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StickyFlow.Data;
using StickyFlow.Services;
using Microsoft.Extensions.Options;
using StickyFlow.Models;
using StickyFlow.Dtos;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAutoMapper(typeof(Program)); 
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Email"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();

// Add Identity
builder.Services.AddIdentity<StickyFlow.Models.ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add to your service collection
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Email"));

// Configure JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
        ValidAudience = builder.Configuration["Jwt:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDefaultFiles(); // Serve index.html as default
app.UseStaticFiles(); // Serve static files from wwwroot

app.UseCors("AllowAll");

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/dashboard"))
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last()
                    ?? context.Request.Cookies["token"];

        if (string.IsNullOrEmpty(token))
        {
            context.Response.Redirect("/login");
            return;
        }

        try
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]);
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:ValidIssuer"],
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:ValidAudience"],
                ValidateLifetime = true
            }, out SecurityToken validatedToken);
        }
        catch
        {
            context.Response.Redirect("/login");
            return;
        }
    }
    await next(context);
});
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html"); // Fallback to index.html for unmatched routes

app.Run();