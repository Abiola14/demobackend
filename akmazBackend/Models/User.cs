namespace AkmazBackend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public DateTime Created_At { get; set; } = DateTime.Now;
        public bool IsTemporaryPassword { get; set; }

        // OTP Password Reset Fields
        public string? OtpCode { get; set; }           // 6-digit OTP
        public DateTime? OtpExpiry { get; set; }       // Expiry time
        public int OtpAttempts { get; set; } = 0;      // ← ADD THIS LINE
    }
}