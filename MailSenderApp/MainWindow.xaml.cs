using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MailSenderApp.Models;
using MailSenderApp.Services;

namespace MailSenderApp
{
    public partial class MainWindow : Window
    {
        private readonly MailService _mail = new();
        private readonly StoreService _store = new();

        private readonly ObservableCollection<AttachmentItem> _attachments = new();
        private readonly ObservableCollection<Recipient> _recipients = new();
        private readonly ObservableCollection<LogEntry> _logView = new();
        private List<LogEntry> _allLogs = new();

        private CancellationTokenSource? _cts;

        // ── Templates ──
        private readonly List<(string Title, string Subject, string Body)> _templates = new()
        {
            ("-- Chọn mẫu --", "", ""),
            ("Thư mời họp",
             "[THÔNG BÁO] Thư mời họp triển khai dự án",
             "<p>Kính gửi các thành viên,</p><p>Trân trọng kính mời các bạn tham dự cuộc họp:</p><ul><li><b>Thời gian:</b> 09:00 — thứ Hai tuần tới</li><li><b>Nội dung:</b> Báo cáo tiến độ đồ án Lập trình mạng</li></ul><p>Rất mong các bạn có mặt đúng giờ!</p>"),
            ("Chào mừng thành viên mới",
             "Chào mừng bạn gia nhập hệ thống!",
             "<h2>Chào mừng bạn!</h2><p>Tài khoản của bạn đã được kích hoạt thành công. Mọi thắc mắc xin phản hồi qua email này.</p>"),
            ("Nhắc nộp báo cáo",
             "[NHẮC NHỞ] Hạn chót nộp báo cáo đồ án",
             "<p>Chào các bạn,</p><p>Hệ thống nhắc nhở thời hạn nộp <b>báo cáo đồ án Lập trình mạng (Đề tài 5)</b> đang đến gần.</p><p>Vui lòng hoàn thiện và nộp đúng hạn!</p>")
        };

        // ══════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();

            LbAttach.ItemsSource = _attachments;
            DgRecipients.ItemsSource = _recipients;
            DgHistory.ItemsSource = _logView;

            CbTemplate.ItemsSource = _templates.Select(t => t.Title).ToList();
            CbTemplate.SelectedIndex = 0;

            LoadConfig();
            LoadHistory();
            UpdateStats();
        }

        // ══════════════════ INIT ═════════════════════════════════

        private void LoadConfig()
        {
            var p = _store.LoadConfig();
            TbServer.Text = p.Server;
            TbPort.Text = p.Port.ToString();
            ChkSsl.IsChecked = p.EnableSsl;
            TbDisplayName.Text = p.DisplayName;
            TbEmail.Text = p.SenderEmail;
            PbPass.Password = p.SenderPassword;
            UpdateServerBadge(p.Server, p.Port);
        }

        private SmtpProfile GetProfile()
        {
            int.TryParse(TbPort.Text.Trim(), out int port);
            if (port <= 0) port = 587;
            return new SmtpProfile
            {
                Server = TbServer.Text.Trim(),
                Port = port,
                EnableSsl = ChkSsl.IsChecked ?? true,
                DisplayName = TbDisplayName.Text.Trim(),
                SenderEmail = TbEmail.Text.Trim(),
                SenderPassword = PbPass.Password.Trim()
            };
        }

        private void UpdateServerBadge(string server, int port)
        {
            TbServerBadge.Text = $"{server} : {port}";
        }

        private void LoadHistory()
        {
            _allLogs = _store.LoadLog();
            FilterLog();
        }

        private void FilterLog()
        {
            string kw = (TbSearch?.Text ?? "").Trim().ToLower();
            _logView.Clear();
            var items = string.IsNullOrEmpty(kw)
                ? _allLogs
                : _allLogs.Where(e =>
                    e.Subject.ToLower().Contains(kw) ||
                    e.To.ToLower().Contains(kw) ||
                    e.Sender.ToLower().Contains(kw));
            foreach (var e in items) _logView.Add(e);
        }

        private void UpdateStats()
        {
            int total = _recipients.Count;
            int ok = _recipients.Count(r => r.Status == "✓ Thành công");
            int fail = _recipients.Count(r => r.Status == "✗ Thất bại");
            TbStats.Text = $"  (Tổng: {total} | ✓ {ok} | ✗ {fail})";
        }

        // ══════════════════ TAB 1: SOẠN & GỬI ═══════════════════

        private void CbTemplate_Changed(object sender, SelectionChangedEventArgs e)
        {
            int i = CbTemplate.SelectedIndex;
            if (i <= 0 || i >= _templates.Count) return;
            var (_, subj, body) = _templates[i];
            TbSubject.Text = subj;
            TbBody.Text = body;
            ChkHtml.IsChecked = true;
        }

        private void BtnAddAttach_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Chọn tệp đính kèm",
                Filter = "Tất cả tệp (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (string f in dlg.FileNames)
                    if (!_attachments.Any(a => a.FilePath == f))
                        _attachments.Add(new AttachmentItem(f));
            }
        }

        private void LbAttach_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
                    if (!_attachments.Any(a => a.FilePath == f))
                        _attachments.Add(new AttachmentItem(f));
        }

        private void BtnRemoveAttach_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AttachmentItem item)
                _attachments.Remove(item);
        }

        private void BtnClearAttach_Click(object sender, RoutedEventArgs e) => _attachments.Clear();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TbTo.Clear(); TbCc.Clear(); TbBcc.Clear();
            TbSubject.Clear(); TbBody.Clear();
            _attachments.Clear();
            CbTemplate.SelectedIndex = 0;
            Status("Đã làm sạch biểu mẫu.");
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string to = TbTo.Text.Trim();
            string subject = TbSubject.Text.Trim();

            if (string.IsNullOrWhiteSpace(to))
            { MessageBox.Show("Vui lòng nhập địa chỉ email người nhận (To)!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(subject))
            { MessageBox.Show("Vui lòng nhập tiêu đề thư!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var p = GetProfile();
            if (string.IsNullOrWhiteSpace(p.SenderEmail) || string.IsNullOrWhiteSpace(p.SenderPassword))
            { MessageBox.Show("Vui lòng vào tab ⚙ Cấu Hình SMTP để nhập Email và Mật khẩu ứng dụng!", "Chưa cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            MailPriority pri = CbPriority.SelectedIndex == 0 ? MailPriority.Low
                             : CbPriority.SelectedIndex == 2 ? MailPriority.High
                             : MailPriority.Normal;

            BtnSend.IsEnabled = false;
            Status($"Đang gửi thư đến {to}…");

            var log = new LogEntry
            {
                Sender = p.SenderEmail,
                To = to,
                Subject = subject,
                Attachments = _attachments.Count
            };

            try
            {
                await _mail.SendOneAsync(p,
                    to, TbCc.Text.Trim(), TbBcc.Text.Trim(),
                    subject, TbBody.Text,
                    ChkHtml.IsChecked ?? false,
                    _attachments.Select(a => a.FilePath));

                log.Ok = true; log.Error = "Gửi thành công";
                Status($"✓ Đã gửi thành công đến {to} lúc {DateTime.Now:HH:mm:ss}");
                MessageBox.Show($"Gửi thư thành công!\nNgười nhận: {to}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Ok = false; log.Error = ex.Message;
                Status($"✗ Gửi thất bại: {ex.Message}");
                MessageBox.Show($"Lỗi gửi thư:\n\n{ex.Message}", "Gửi thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSend.IsEnabled = true;
                _store.AppendLog(log);
                LoadHistory();
            }
        }

        // ══════════════════ TAB 2: HÀNG LOẠT ════════════════════

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file danh sách (CSV hoặc TXT)",
                Filter = "File dữ liệu (*.csv;*.txt)|*.csv;*.txt|Tất cả (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            var list = _store.ParseCsv(dlg.FileName);
            _recipients.Clear();
            foreach (var r in list) _recipients.Add(r);
            UpdateStats();
            Status($"Đã nạp {list.Count} người nhận từ {Path.GetFileName(dlg.FileName)}.");
        }

        private void BtnSampleData_Click(object sender, RoutedEventArgs e)
        {
            // Thử tìm file sample trong thư mục exe
            string[] paths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_recipients.csv"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "sample_recipients.csv")
            };
            string? found = paths.FirstOrDefault(File.Exists);

            if (found != null)
            {
                var list = _store.ParseCsv(found);
                _recipients.Clear();
                foreach (var r in list) _recipients.Add(r);
            }
            else
            {
                // Tạo thẳng trong bộ nhớ
                _recipients.Clear();
                _recipients.Add(new Recipient { STT = 1, Name = "Nguyễn Văn An", Email = "nguyenvanan@gmail.com", Note = "Trưởng nhóm CNTT" });
                _recipients.Add(new Recipient { STT = 2, Name = "Trần Thị Bình", Email = "binhtt@outlook.com", Note = "Lập trình viên Backend" });
                _recipients.Add(new Recipient { STT = 3, Name = "Lê Hoàng Cường", Email = "cuonglh@company.vn", Note = "Kiểm thử QA" });
                _recipients.Add(new Recipient { STT = 4, Name = "Phạm Thu Dung", Email = "dungpt@gmail.com", Note = "Quản trị mạng" });
                _recipients.Add(new Recipient { STT = 5, Name = "Hoàng Minh Đức", Email = "duchm@edu.vn", Note = "Giảng viên hướng dẫn" });
            }
            UpdateStats();
            Status($"Đã nạp {_recipients.Count} người nhận mẫu.");
        }

        private async void BtnStartBulk_Click(object sender, RoutedEventArgs e)
        {
            if (_recipients.Count == 0)
            { MessageBox.Show("Danh sách người nhận đang trống!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var p = GetProfile();
            if (string.IsNullOrWhiteSpace(p.SenderEmail) || string.IsNullOrWhiteSpace(p.SenderPassword))
            { MessageBox.Show("Vui lòng cấu hình SMTP trước khi gửi hàng loạt!", "Chưa cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            int delay = CbDelay.SelectedIndex switch { 0 => 0, 1 => 2, 2 => 5, 3 => 10, 4 => 30, _ => 2 };

            PbBulk.Value = 0;
            TbProgress.Text = "Tiến độ: 0%";
            TbCurrent.Text = "Đang khởi tạo…";
            BtnStartBulk.IsEnabled = false;
            BtnStopBulk.IsEnabled = true;

            _cts = new CancellationTokenSource();

            var progress = new Progress<(int done, int total)>(r =>
            {
                double pct = (double)r.done / r.total * 100;
                PbBulk.Value = pct;
                TbProgress.Text = $"Tiến độ: {r.done}/{r.total} ({pct:F0}%)";
                var cur = _recipients.ElementAtOrDefault(r.done - 1);
                if (cur != null) TbCurrent.Text = $"Đã xử lý: {cur.Name} ({cur.Email})";
                UpdateStats();
            });

            try
            {
                await _mail.SendBulkAsync(p, _recipients,
                    TbBulkSubject.Text, TbBulkBody.Text, true,
                    _attachments.Select(a => a.FilePath),
                    delay, progress, _cts.Token);

                Status("✓ Hoàn tất gửi email hàng loạt!");
                MessageBox.Show("Đã hoàn tất gửi email hàng loạt!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                Status("⏹ Đã dừng tiến trình gửi hàng loạt.");
                MessageBox.Show("Đã dừng tiến trình.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Status($"✗ Lỗi: {ex.Message}");
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnStartBulk.IsEnabled = true;
                BtnStopBulk.IsEnabled = false;
                UpdateStats();
                _store.AppendLog(new LogEntry
                {
                    Sender = p.SenderEmail,
                    To = $"[Bulk] {_recipients.Count} người nhận",
                    Subject = TbBulkSubject.Text,
                    Ok = _recipients.Any(r => r.Status == "✓ Thành công"),
                    Error = $"✓{_recipients.Count(r => r.Status == "✓ Thành công")} ✗{_recipients.Count(r => r.Status == "✗ Thất bại")}"
                });
                LoadHistory();
            }
        }

        private void BtnStopBulk_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BtnStopBulk.IsEnabled = false;
            TbCurrent.Text = "Đang hủy…";
        }

        // ══════════════════ TAB 3: CẤU HÌNH ═════════════════════

        private void CbPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (TbServer == null) return;
            switch (CbPreset.SelectedIndex)
            {
                case 0: TbServer.Text = "smtp.gmail.com";        TbPort.Text = "587"; ChkSsl.IsChecked = true;  break;
                case 1: TbServer.Text = "smtp.office365.com";    TbPort.Text = "587"; ChkSsl.IsChecked = true;  break;
                case 2: TbServer.Text = "smtp.mail.yahoo.com";   TbPort.Text = "587"; ChkSsl.IsChecked = true;  break;
                case 3: TbServer.Text = "localhost";              TbPort.Text = "25";  ChkSsl.IsChecked = false; break;
            }
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var p = GetProfile();
            BtnTest.IsEnabled = false;
            Status($"Đang kiểm tra kết nối tới {p.Server}:{p.Port}…");

            var (ok, msg) = await _mail.TestAsync(p);
            BtnTest.IsEnabled = true;

            DotStatus.Fill = ok
                ? (SolidColorBrush)FindResource("SuccessBrush")
                : (SolidColorBrush)FindResource("DangerBrush");

            Status(ok ? "✓ Kết nối thành công!" : "✗ Kết nối thất bại.");
            MessageBox.Show(msg,
                ok ? "Kiểm tra thành công" : "Kiểm tra thất bại",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void BtnSaveCfg_Click(object sender, RoutedEventArgs e)
        {
            var p = GetProfile();
            _store.SaveConfig(p);
            UpdateServerBadge(p.Server, p.Port);
            Status("✓ Đã lưu cấu hình SMTP.");
            MessageBox.Show("Cấu hình SMTP đã được lưu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ══════════════════ TAB 4: LỊCH SỬ ══════════════════════

        private void TbSearch_Changed(object sender, TextChangedEventArgs e) => FilterLog();

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) { LoadHistory(); Status("Đã làm mới nhật ký."); }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_allLogs.Count == 0)
            { MessageBox.Show("Lịch sử đang trống!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"EmailHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                _store.ExportCsv(dlg.FileName, _allLogs);
                Status($"✓ Đã xuất CSV: {dlg.FileName}");
                MessageBox.Show("Xuất file CSV thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Xóa toàn bộ lịch sử?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _store.ClearLog(); LoadHistory(); Status("Đã xóa toàn bộ lịch sử.");
            }
        }

        // ══════════════════ HELPER ═══════════════════════════════
        private void Status(string msg) => TbStatus.Text = msg;
    }
}
