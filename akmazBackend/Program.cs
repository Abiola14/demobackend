
using Microsoft.EntityFrameworkCore;
using AkmazBackend.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==================================================
// 1. SERVICES
// ==================================================

builder.Services.AddControllers();

// ==================================================
// 2. MYSQL DATABASE CONFIGURATION (RAILWAY)
// ==================================================

var host = builder.Configuration["MYSQLHOST"];
var port = builder.Configuration["MYSQLPORT"];
var user = builder.Configuration["MYSQLUSER"];
var password = builder.Configuration["MYSQLPASSWORD"];
var database = builder.Configuration["MYSQLDATABASE"];

if (string.IsNullOrWhiteSpace(host) ||
    string.IsNullOrWhiteSpace(port) ||
    string.IsNullOrWhiteSpace(user) ||
    string.IsNullOrWhiteSpace(password) ||
    string.IsNullOrWhiteSpace(database))
{
    throw new InvalidOperationException(
        "Railway MySQL variables are missing. Check MYSQLHOST, MYSQLPORT, MYSQLUSER, MYSQLPASSWORD and MYSQLDATABASE."
    );
}

if (!uint.TryParse(port, out var mysqlPort))
{
    throw new InvalidOperationException(
        "MYSQLPORT must be a valid port number."
    );
}

var mysqlConnection = new MySqlConnectionStringBuilder
{
    Server = host,
    Port = mysqlPort,
    UserID = user,
    Password = password,
    Database = database,
    SslMode = MySqlSslMode.Preferred,
    ConnectionTimeout = 30
};

var connectionString = mysqlConnection.ConnectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 33))
    )
);

// ==================================================
// 3. CORS CONFIGURATION
// ==================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "https://demoakmaz.vercel.app",
                "http://localhost:5173",
                "http://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ==================================================
// 4. JWT AUTHENTICATION
// ==================================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT signing key is missing. Configure Jwt__Key in Railway."
    );
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must be at least 32 bytes."
    );
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                )
            };
    });

builder.Services.AddAuthorization();

// ==================================================
// 5. BUILD APPLICATION
// ==================================================

var app = builder.Build();

// ==================================================
// 6. MIDDLEWARE
// ==================================================

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseAuthorization();

// ==================================================
// 7. HEALTH CHECK
// ==================================================

app.MapGet("/", () => Results.Ok(new
{
    status = "Online",
    application = "Akmaz Backend",
    message = "API is running successfully"
}));

app.MapControllers();

// ==================================================
// 8. RAILWAY PORT
// ==================================================

var railwayPort =
    Environment.GetEnvironmentVariable("PORT") ?? "8080";

app.Run($"http://0.0.0.0:{railwayPort}");
