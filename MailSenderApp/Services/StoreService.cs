using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MailSenderApp.Models;

namespace MailSenderApp.Services
{
    public class StoreService
    {
        private readonly string _dir;
        private readonly string _cfgPath;
        private readonly string _logPath;

        public StoreService()
        {
            _dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MailSenderApp");
            Directory.CreateDirectory(_dir);
            _cfgPath = Path.Combine(_dir, "config.json");
            _logPath = Path.Combine(_dir, "history.json");
        }

        // ── Config ──
        public SmtpProfile LoadConfig()
        {
            try
            {
                if (File.Exists(_cfgPath))
                    return JsonSerializer.Deserialize<SmtpProfile>(File.ReadAllText(_cfgPath)) ?? new();
            }
            catch { }
            return new SmtpProfile();
        }

        public void SaveConfig(SmtpProfile p)
        {
            try
            {
                File.WriteAllText(_cfgPath,
                    JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ── History ──
        public List<LogEntry> LoadLog()
        {
            try
            {
                if (File.Exists(_logPath))
                    return JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(_logPath)) ?? new();
            }
            catch { }
            return new();
        }

        public void AppendLog(LogEntry e)
        {
            var list = LoadLog();
            list.Insert(0, e);
            if (list.Count > 500) list.RemoveRange(500, list.Count - 500);
            try
            {
                File.WriteAllText(_logPath,
                    JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void ClearLog()
        {
            try { File.Delete(_logPath); } catch { }
        }

        // ── CSV parse ──
        public List<Recipient> ParseCsv(string path)
        {
            var result = new List<Recipient>();
            if (!File.Exists(path)) return result;

            int idx = 1;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Bỏ dòng header
                if (line.StartsWith("Tên", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Email", StringComparison.OrdinalIgnoreCase)) continue;

                char sep = line.Contains(';') ? ';' : line.Contains('\t') ? '\t' : ',';
                var parts = line.Split(sep);

                string name = "", email = "", note = "";
                if (parts.Length == 1) { email = parts[0].Trim(); name = email.Split('@')[0]; }
                else if (parts.Length == 2)
                {
                    if (parts[0].Contains('@')) { email = parts[0].Trim(); name = parts[1].Trim(); }
                    else { name = parts[0].Trim(); email = parts[1].Trim(); }
                }
                else
                {
                    name = parts[0].Trim();
                    email = parts[1].Trim();
                    note = parts[2].Trim();
                }

                if (!string.IsNullOrWhiteSpace(email))
                    result.Add(new Recipient { STT = idx++, Name = name, Email = email, Note = note });
            }
            return result;
        }

        // ── Export CSV ──
        public void ExportCsv(string path, IEnumerable<LogEntry> logs)
        {
            var sb = new StringBuilder("Mã,Thời gian,Người gửi,Người nhận,Tiêu đề,File đính kèm,Trạng thái,Lỗi\n");
            foreach (var e in logs)
                sb.AppendLine($"{e.Id},{e.TimeStr},{e.Sender},\"{e.To}\",\"{e.Subject}\",{e.Attachments},{e.StatusLabel},\"{e.Error}\"");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}

