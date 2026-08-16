using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ================= RAILWAY PORT =================
// Use Railway's PORT environment variable.
// Falls back to 8000 when running locally.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ================= SERVICES =================

builder.Services.AddControllers();

// ================= DATABASE =================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 33))
    )
);

// ================= CORS =================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ================= JWT AUTHENTICATION =================

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    "THIS_IS_MY_SUPER_SECRET_KEY_12345"
                )
            )
        };
    });

builder.Services.AddAuthorization();

// ================= BUILD APP =================

var app = builder.Build();

// ================= MIDDLEWARE =================

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseAuthorization();

// ================= CONTROLLERS =================

app.MapControllers();

// ================= START APPLICATION =================

app.Run();  