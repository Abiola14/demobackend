namespace AkmazBackend.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "auditor";

        public DateTime Created_At { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public bool IsTemporaryPassword { get; set; }

        // OTP Password Reset
        public string? OtpCode { get; set; }

        public DateTime? OtpExpiry { get; set; }

        public int OtpAttempts { get; set; } = 0;
    }
}