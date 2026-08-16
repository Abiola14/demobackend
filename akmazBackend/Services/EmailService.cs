using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace AkmazBackend.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendOtp(string toEmail, string otp)
        {
            var emailSettings = _config.GetSection("EmailSettings");
            var smtp = new SmtpClient(emailSettings["SmtpServer"])
            {
                Port = int.Parse(emailSettings["Port"]!),
                Credentials = new NetworkCredential(emailSettings["Username"], emailSettings["Password"]),
                EnableSsl = true
            };

            var mail = new MailMessage();
            mail.From = new MailAddress(emailSettings["From"]);
            mail.To.Add(toEmail);
            mail.Subject = "Your OTP Code";
            mail.Body = $"Your OTP code is: {otp}. It expires in 10 minutes.";

            smtp.Send(mail);
        }
    }
}
