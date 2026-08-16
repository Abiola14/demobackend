using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AkmazBackend.Data;
using AkmazBackend.Models;
using BCrypt.Net;
using System.Net.Mail;
using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AkmazBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        // ✅ Must match exactly what's in Program.cs
        private const string JWT_SECRET = "THIS_IS_MY_SUPER_SECRET_KEY_12345";

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // ======== REGISTER ========
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (_context.tblUsers.Any(u => u.Username == model.Username))
                return BadRequest("Username already exists");
            if (_context.tblUsers.Any(u => u.Email == model.Email))
                return BadRequest("Email already exists");

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = model.Role,
                IsTemporaryPassword = false
            };
            _context.tblUsers.Add(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "User registered successfully" });
        }

        // ======== LOGIN — now generates a REAL JWT ========
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest model)
        {
            var user = _context.tblUsers.FirstOrDefault(u => u.Username == model.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
                return Unauthorized("Invalid username or password");

            // ✅ Build real JWT claims — role must be ClaimTypes.Role so [Authorize(Roles=...)] works
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToLower()), // "admin" or "auditor"
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JWT_SECRET));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8), // token valid for 8 hours
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = tokenString,   // ✅ real JWT string
                username = user.Username,
                role = user.Role
            });
        }

        // ======== REQUEST OTP ========
        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp([FromBody] EmailRequest model)
        {
            var user = _context.tblUsers.FirstOrDefault(u => u.Email == model.Email);
            if (user == null)
                return Ok(new { message = "If email exists, OTP sent." });

            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            user.OtpAttempts = 0;
            await _context.SaveChangesAsync();

            try
            {
                await SendOtpEmailAsync(user.Email, user.Username, otp);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to send email", details = ex.Message });
            }

            return Ok(new { message = "OTP sent to your email!" });
        }

        // ======== VERIFY OTP ========
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest model)
        {
            var user = _context.tblUsers.FirstOrDefault(u => u.Email == model.Email);
            if (user == null) return BadRequest("Email not found");
            if (user.OtpAttempts >= 5) return BadRequest("Too many attempts.");

            if (user.OtpCode != model.Otp)
            {
                user.OtpAttempts++;
                await _context.SaveChangesAsync();
                return BadRequest("Invalid OTP");
            }

            if (user.OtpExpiry < DateTime.UtcNow)
            {
                user.OtpCode = null;
                await _context.SaveChangesAsync();
                return BadRequest("OTP expired");
            }

            return Ok(new { message = "OTP verified" });
        }

        // ======== RESET PASSWORD ========
        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordOtp([FromBody] ResetPasswordOtpRequest model)
        {
            var user = _context.tblUsers.FirstOrDefault(u => u.Email == model.Email);
            if (user == null) return BadRequest("Email not found");
            if (user.OtpCode != model.Otp || user.OtpExpiry < DateTime.UtcNow)
                return BadRequest("Invalid or expired OTP");

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.OtpCode = null;
            user.OtpExpiry = null;
            user.OtpAttempts = 0;
            user.IsTemporaryPassword = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successful", role = user.Role });
        }

        // ======== EMAIL SENDER ========
        private async Task SendOtpEmailAsync(string toEmail, string username, string otp)
        {
            var fromEmail = "abiolalingard200@gmail.com";
            var appPassword = "qisd oalz xlmm yyqa";

            var message = new MailMessage();
            message.From = new MailAddress(fromEmail, "Akmaz App");
            message.To.Add(toEmail);
            message.Subject = "Your Password Reset OTP - Akmaz";
            message.IsBodyHtml = true;
            message.Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px; background: #f9f9f9;'>
                    <h2 style='color: #1e40af; text-align: center;'>Password Reset Request</h2>
                    <p>Hi <strong>{username}</strong>,</p>
                    <p>You requested a password reset. Here is your 6-digit OTP:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <span style='font-size: 36px; font-weight: bold; letter-spacing: 10px; color: #1e40af; padding: 15px 25px; background: white; border-radius: 10px; border: 3px dashed #1e40af;'>
                            {otp}
                        </span>
                    </div>
                    <p><strong>This OTP expires in 10 minutes.</strong></p>
                    <p style='color: #666; font-size: 13px;'>If you didn't request this, just ignore this email.</p>
                    <hr>
                    <p style='font-size: 12px; color: #999; text-align: center;'>© 2025 Akmaz App. All rights reserved.</p>
                </div>";

            using var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
            };

            await client.SendMailAsync(message);
        }

        // ======== DTOs ========
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
        public class RegisterRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Role { get; set; } = "auditor";
        }
        public class EmailRequest { public string Email { get; set; } = string.Empty; }
        public class VerifyOtpRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
        }
        public class ResetPasswordOtpRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}