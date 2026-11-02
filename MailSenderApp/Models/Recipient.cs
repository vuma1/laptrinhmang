using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MailSenderApp.Models
{
    public class Recipient : INotifyPropertyChanged
    {
        private string _status = "Sẵn sàng";
        private string _note2 = "";
        private string _sentAt = "";

        public int STT { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Note { get; set; } = "";

        public string Status
        {
            get => _status;
            set { _status = value; OnProp(); }
        }

        public string Note2
        {
            get => _note2;
            set { _note2 = value; OnProp(); }
        }

        public string SentAt
        {
            get => _sentAt;
            set { _sentAt = value; OnProp(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnProp([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}

