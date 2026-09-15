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
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AkmazBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        private const string JWT_SECRET =
            "THIS_IS_MY_SUPER_SECRET_KEY_12345";

        // ============================================================
        // BUILT-IN SUPER ADMIN
        // ============================================================

        private const string SUPER_ADMIN_USERNAME = "admin";
        private const string SUPER_ADMIN_PASSWORD = "admin123";

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // REGISTER
        // ============================================================

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest model)
        {
            if (model == null)
                return BadRequest("Registration data is required.");

            if (string.IsNullOrWhiteSpace(model.Username))
                return BadRequest("Username is required.");

            if (string.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Email is required.");

            if (string.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Password is required.");

            // Built-in Super Admin username is reserved
            if (model.Username.Trim().Equals(
                SUPER_ADMIN_USERNAME,
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "This username is reserved by the system."
                );
            }

            if (_context.tblUsers.Any(
                u => u.Username.ToLower() == model.Username.ToLower()))
            {
                return BadRequest("Username already exists.");
            }

            if (_context.tblUsers.Any(
                u => u.Email.ToLower() == model.Email.ToLower()))
            {
                return BadRequest("Email already exists.");
            }

            // Public registration can NEVER create Super Admin.
            var role = model.Role?.Trim().ToLower();

            if (role != "admin" && role != "auditor")
            {
                role = "auditor";
            }

            var user = new User
            {
                Username = model.Username.Trim(),
                Email = model.Email.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = role,
                IsTemporaryPassword = false,
                IsActive = true
            };

            _context.tblUsers.Add(user);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User registered successfully."
            });
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return Unauthorized("Invalid username or password.");
            }

            // ========================================================
            // BUILT-IN SUPER ADMIN LOGIN
            // ========================================================

            if (
                model.Username.Trim().Equals(
                    SUPER_ADMIN_USERNAME,
                    StringComparison.OrdinalIgnoreCase
                )
                &&
                model.Password == SUPER_ADMIN_PASSWORD
            )
            {
                var superAdminToken = GenerateJwtToken(
                    SUPER_ADMIN_USERNAME,
                    "superadmin"
                );

                return Ok(new
                {
                    token = superAdminToken,
                    username = SUPER_ADMIN_USERNAME,
                    role = "superadmin"
                });
            }

            // ========================================================
            // NORMAL DATABASE USER LOGIN
            // ========================================================

            var user = _context.tblUsers
                .FirstOrDefault(u =>
                    u.Username == model.Username);

            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(
                    model.Password,
                    user.Password))
            {
                return Unauthorized(
                    "Invalid username or password."
                );
            }

            if (!user.IsActive)
            {
                return Unauthorized(
                    "Your account has been deactivated. Please contact the Super Admin."
                );
            }

            var role = (user.Role ?? "user")
                .Trim()
                .ToLower();

            var token = GenerateJwtToken(
                user.Username,
                role
            );

            return Ok(new
            {
                token = token,
                username = user.Username,
                role = role
            });
        }

        // ============================================================
        // GENERATE JWT TOKEN
        // ============================================================

        private string GenerateJwtToken(
            string username,
            string role)
        {
            var claims = new[]
            {
                new Claim(
                    ClaimTypes.Name,
                    username
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                ),

                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    username
                ),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(JWT_SECRET)
            );

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // ============================================================
        // SUPER ADMIN — LIST USERS
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpGet("list")]
        public async Task<IActionResult> ListUsers()
        {
            var users = await _context.tblUsers
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.Created_At,
                    u.IsActive,
                    u.IsTemporaryPassword
                })
                .ToListAsync();

            return Ok(users);
        }

        // ============================================================
        // SUPER ADMIN — CREATE USER
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPost("admin-create")]
        public async Task<IActionResult> AdminCreateUser(
            [FromBody] AdminCreateUserRequest model)
        {
            if (model == null)
                return BadRequest("User data is required.");

            if (string.IsNullOrWhiteSpace(model.Username))
                return BadRequest("Username is required.");

            if (string.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Email is required.");

            if (string.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Password is required.");

            // Built-in Super Admin username is reserved
            if (model.Username.Trim().Equals(
                SUPER_ADMIN_USERNAME,
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "This username is reserved by the system."
                );
            }

            var role = model.Role?.Trim().ToLower();

            var validRoles = new[]
            {
                "admin",
                "auditor",
                "superadmin"
            };

            if (!validRoles.Contains(role))
                return BadRequest("Invalid role.");

            if (_context.tblUsers.Any(
                u => u.Username.ToLower() ==
                     model.Username.ToLower()))
            {
                return BadRequest("Username already exists.");
            }

            if (_context.tblUsers.Any(
                u => u.Email.ToLower() ==
                     model.Email.ToLower()))
            {
                return BadRequest("Email already exists.");
            }

            var user = new User
            {
                Username = model.Username.Trim(),
                Email = model.Email.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(
                    model.Password
                ),
                Role = role,
                IsTemporaryPassword = false,
                IsActive = true
            };

            _context.tblUsers.Add(user);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User created successfully.",
                userId = user.Id
            });
        }

        // ============================================================
        // SUPER ADMIN — CHANGE ROLE
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPut("change-role/{id}")]
        public async Task<IActionResult> ChangeRole(
            int id,
            [FromBody] ChangeRoleRequest model)
        {
            var user = await _context.tblUsers.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            var role = model.Role?.Trim().ToLower();

            var validRoles = new[]
            {
                "admin",
                "auditor",
                "superadmin"
            };

            if (!validRoles.Contains(role))
                return BadRequest("Invalid role.");

            // Prevent Super Admin from accidentally removing
            // their own Super Admin role.
            var currentUsername =
                User.Identity?.Name;

            if (
                user.Username.Equals(
                    currentUsername,
                    StringComparison.OrdinalIgnoreCase
                )
                &&
                role != "superadmin"
            )
            {
                return BadRequest(
                    "You cannot remove your own Super Admin role."
                );
            }

            user.Role = role;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User role updated successfully."
            });
        }

        // ============================================================
        // SUPER ADMIN — ACTIVATE / DEACTIVATE USER
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPut("toggle-status/{id}")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.tblUsers.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            var currentUsername =
                User.Identity?.Name;

            if (
                user.Username.Equals(
                    currentUsername,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return BadRequest(
                    "You cannot deactivate your own account."
                );
            }

            user.IsActive = !user.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = user.IsActive
                    ? "User activated successfully."
                    : "User deactivated successfully.",

                isActive = user.IsActive
            });
        }

        // ============================================================
        // SUPER ADMIN — DELETE USER
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.tblUsers.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            var currentUsername =
                User.Identity?.Name;

            if (
                user.Username.Equals(
                    currentUsername,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return BadRequest(
                    "You cannot delete your own account."
                );
            }

            _context.tblUsers.Remove(user);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User deleted successfully."
            });
        }

        // ============================================================
        // SUPER ADMIN — RESET PASSWORD
        // ============================================================

        [Authorize(Roles = "superadmin")]
        [HttpPost("admin-reset")]
        public async Task<IActionResult> AdminResetPassword(
            [FromBody] AdminResetPasswordRequest model)
        {
            var user = await _context.tblUsers
                .FirstOrDefaultAsync(
                    u => u.Username == model.Username
                );

            if (user == null)
                return NotFound("User not found.");

            if (string.IsNullOrWhiteSpace(
                model.NewPassword))
            {
                return BadRequest(
                    "New password is required."
                );
            }

            user.Password =
                BCrypt.Net.BCrypt.HashPassword(
                    model.NewPassword
                );

            user.IsTemporaryPassword = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Password reset successfully."
            });
        }

        // ============================================================
        // REQUEST OTP
        // ============================================================

        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp(
            [FromBody] EmailRequest model)
        {
            var user = _context.tblUsers
                .FirstOrDefault(
                    u => u.Email == model.Email
                );

            if (user == null)
            {
                return Ok(
                    new
                    {
                        message =
                            "If email exists, OTP sent."
                    }
                );
            }

            var otp =
                new Random()
                    .Next(100000, 999999)
                    .ToString();

            user.OtpCode = otp;

            user.OtpExpiry =
                DateTime.UtcNow.AddMinutes(10);

            user.OtpAttempts = 0;

            await _context.SaveChangesAsync();

            try
            {
                await SendOtpEmailAsync(
                    user.Email,
                    user.Username,
                    otp
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        error = "Failed to send email",
                        details = ex.Message
                    }
                );
            }

            return Ok(
                new
                {
                    message =
                        "OTP sent to your email!"
                }
            );
        }

        // ============================================================
        // VERIFY OTP
        // ============================================================

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] VerifyOtpRequest model)
        {
            var user = _context.tblUsers
                .FirstOrDefault(
                    u => u.Email == model.Email
                );

            if (user == null)
                return BadRequest("Email not found");

            if (user.OtpAttempts >= 5)
                return BadRequest("Too many attempts.");

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

            return Ok(
                new
                {
                    message = "OTP verified"
                }
            );
        }

        // ============================================================
        // RESET PASSWORD
        // ============================================================

        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordOtp(
            [FromBody] ResetPasswordOtpRequest model)
        {
            var user = _context.tblUsers
                .FirstOrDefault(
                    u => u.Email == model.Email
                );

            if (user == null)
                return BadRequest("Email not found");

            if (
                user.OtpCode != model.Otp ||
                user.OtpExpiry < DateTime.UtcNow
            )
            {
                return BadRequest(
                    "Invalid or expired OTP"
                );
            }

            user.Password =
                BCrypt.Net.BCrypt.HashPassword(
                    model.NewPassword
                );

            user.OtpCode = null;
            user.OtpExpiry = null;
            user.OtpAttempts = 0;
            user.IsTemporaryPassword = false;

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    message =
                        "Password reset successful",

                    role = user.Role
                }
            );
        }

        // ============================================================
        // EMAIL
        // ============================================================

        private async Task SendOtpEmailAsync(
            string toEmail,
            string username,
            string otp)
        {
            var fromEmail =
                "abiolalingard200@gmail.com";

            var appPassword =
                "qisd oalz xlmm yyqa";

            var message = new MailMessage();

            message.From =
                new MailAddress(
                    fromEmail,
                    "Akmaz App"
                );

            message.To.Add(toEmail);

            message.Subject =
                "Your Password Reset OTP - Akmaz";

            message.IsBodyHtml = true;

            message.Body = $@"
                <div style='font-family:Arial'>

                    <h2>Akmaz Password Reset</h2>

                    <p>
                        Hi <strong>{username}</strong>,
                    </p>

                    <p>
                        Your password reset OTP is:
                    </p>

                    <h1>{otp}</h1>

                    <p>
                        This OTP expires in 10 minutes.
                    </p>

                </div>";

            using var client =
                new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,

                    Credentials =
                        new NetworkCredential(
                            fromEmail,
                            appPassword
                        ),

                    EnableSsl = true
                };

            await client.SendMailAsync(message);
        }

        // ============================================================
        // DTOs
        // ============================================================

        public class LoginRequest
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Role { get; set; } = "auditor";
        }

        public class AdminCreateUserRequest
        {
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Role { get; set; } = "auditor";
        }

        public class ChangeRoleRequest
        {
            public string Role { get; set; } = "";
        }

        public class AdminResetPasswordRequest
        {
            public string Username { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }

        public class EmailRequest
        {
            public string Email { get; set; } = "";
        }

        public class VerifyOtpRequest
        {
            public string Email { get; set; } = "";
            public string Otp { get; set; } = "";
        }

        public class ResetPasswordOtpRequest
        {
            public string Email { get; set; } = "";
            public string Otp { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }
    }
}