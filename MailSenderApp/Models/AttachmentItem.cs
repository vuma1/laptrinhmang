using System.IO;

namespace MailSenderApp.Models
{
    public class AttachmentItem
    {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public long Bytes { get; }

        public string SizeLabel
        {
            get
            {
                if (Bytes < 1024) return $"{Bytes} B";
                if (Bytes < 1024 * 1024) return $"{Bytes / 1024.0:F1} KB";
                return $"{Bytes / 1048576.0:F2} MB";
            }
        }

        public AttachmentItem(string path)
        {
            FilePath = path;
            if (File.Exists(path))
                Bytes = new FileInfo(path).Length;
        }
    }
}

