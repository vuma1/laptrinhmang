using System;

namespace MailSenderApp.Models
{
    public class LogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public DateTime Time { get; set; } = DateTime.Now;
        public string TimeStr => Time.ToString("dd/MM/yyyy HH:mm:ss");
        public string Sender { get; set; } = "";
        public string To { get; set; } = "";
        public string Subject { get; set; } = "";
        public int Attachments { get; set; }
        public bool Ok { get; set; }
        public string StatusLabel => Ok ? "✓ Thành công" : "✗ Thất bại";
        public string Error { get; set; } = "";
    }
}

