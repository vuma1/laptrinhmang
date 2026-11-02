using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailSenderApp.Models;

namespace MailSenderApp.Services
{
    public class MailService
    {
        // ─── Kiểm tra kết nối TCP + SMTP Authentication ───────────────────────
        public async Task<(bool Ok, string Msg)> TestAsync(SmtpProfile p)
        {
            // Bước 1: TCP ping
            try
            {
                using var tcp = new TcpClient();
                var t = tcp.ConnectAsync(p.Server, p.Port);
                if (await Task.WhenAny(t, Task.Delay(p.TimeoutMs)) != t)
                    return (false, $"Timeout! Không kết nối được {p.Server}:{p.Port} sau {p.TimeoutMs / 1000}s.\nKiểm tra địa chỉ, cổng hoặc tường lửa.");
                await t; // ném lỗi nếu có
            }
            catch (SocketException ex)
            {
                return (false, $"Lỗi Socket TCP: {ex.Message}");
            }

            // Bước 2: Auth (nếu đã có email + password)
            if (string.IsNullOrWhiteSpace(p.SenderEmail) || string.IsNullOrWhiteSpace(p.SenderPassword))
                return (true, $"Kết nối TCP tới {p.Server}:{p.Port} thành công!\n(Chưa kiểm tra xác thực vì chưa nhập email/mật khẩu)");

            try
            {
                using var client = Build(p);
                var msg = new MailMessage
                {
                    From = new MailAddress(p.SenderEmail, p.DisplayName, Encoding.UTF8),
                    Subject = "[TEST] Kiểm tra kết nối SMTP",
                    Body = $"Kết nối SMTP {p.Server}:{p.Port} thành công lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
                    BodyEncoding = Encoding.UTF8
                };
                msg.To.Add(p.SenderEmail);
                await client.SendMailAsync(msg);
                return (true, $"Thành công! Đã gửi email kiểm tra về chính hòm thư {p.SenderEmail}.");
            }
            catch (SmtpException ex)
            {
                string tip = p.Server.Contains("gmail", StringComparison.OrdinalIgnoreCase)
                    ? "\n\n💡 Gmail yêu cầu dùng App Password (16 ký tự).\nVào: myaccount.google.com/apppasswords để tạo."
                    : "";
                return (false, $"Lỗi xác thực SMTP ({ex.StatusCode}): {ex.Message}{tip}");
            }
        }

        // ─── Gửi 1 email (To/CC/BCC + đính kèm) ──────────────────────────────
        public async Task SendOneAsync(SmtpProfile p, string to, string cc, string bcc,
            string subject, string body, bool isHtml, IEnumerable<string> attachments)
        {
            using var client = Build(p);
            using var msg = Compose(p, to, cc, bcc, subject, body, isHtml, attachments);
            await client.SendMailAsync(msg);
        }

        // ─── Gửi hàng loạt ────────────────────────────────────────────────────
        public async Task SendBulkAsync(
            SmtpProfile p,
            ObservableCollection<Recipient> list,
            string subjectTpl, string bodyTpl, bool isHtml,
            IEnumerable<string> attachments,
            int delaySec,
            IProgress<(int done, int total)> progress,
            CancellationToken ct)
        {
            int total = list.Count, done = 0;
            using var client = Build(p);

            foreach (var r in list)
            {
                ct.ThrowIfCancellationRequested();
                r.Status = "Đang gửi...";

                try
                {
                    string subj = Fill(subjectTpl, r);
                    string body = Fill(bodyTpl, r);
                    using var msg = Compose(p, r.Email, "", "", subj, body, isHtml, attachments);
                    // Ghi đè From.DisplayName theo tên người nhận nếu muốn
                    await client.SendMailAsync(msg, ct);

                    r.Status = "✓ Thành công";
                    r.SentAt = DateTime.Now.ToString("HH:mm:ss");
                    r.Note2 = "OK";
                }
                catch (OperationCanceledException)
                {
                    r.Status = "Đã hủy";
                    throw;
                }
                catch (Exception ex)
                {
                    r.Status = "✗ Thất bại";
                    r.Note2 = ex.Message;
                }

                done++;
                progress?.Report((done, total));

                if (done < total && delaySec > 0)
                    await Task.Delay(delaySec * 1000, ct);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private static SmtpClient Build(SmtpProfile p) => new SmtpClient(p.Server, p.Port)
        {
            EnableSsl = p.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = p.TimeoutMs,
            Credentials = new NetworkCredential(p.SenderEmail, p.SenderPassword)
        };

        private static MailMessage Compose(SmtpProfile p, string to, string cc, string bcc,
            string subject, string body, bool isHtml, IEnumerable<string> attachments)
        {
            var msg = new MailMessage
            {
                From = new MailAddress(p.SenderEmail, p.DisplayName, Encoding.UTF8),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            AddAddrs(msg.To, to);
            AddAddrs(msg.CC, cc);
            AddAddrs(msg.Bcc, bcc);
            foreach (var f in attachments)
                if (File.Exists(f)) msg.Attachments.Add(new Attachment(f));
            return msg;
        }

        private static void AddAddrs(MailAddressCollection col, string raw)
        {
            foreach (var a in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = a.Trim();
                if (!string.IsNullOrEmpty(t)) col.Add(t);
            }
        }

        private static string Fill(string tpl, Recipient r)
            => tpl
                .Replace("{Name}", r.Name)
                .Replace("{Email}", r.Email)
                .Replace("{Note}", r.Note)
                .Replace("{Date}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("{Time}", DateTime.Now.ToString("HH:mm"));
    }
}

