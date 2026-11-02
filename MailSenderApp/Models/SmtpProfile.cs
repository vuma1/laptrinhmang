using System;

namespace MailSenderApp.Models
{
    public class SmtpProfile
    {
        public string Server { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
        public string DisplayName { get; set; } = "Mail Sender App";
        public int TimeoutMs { get; set; } = 30000;
    }
}

