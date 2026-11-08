using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MAX30102_WPF.Logging;

namespace MAX30102_WPF.Ui
{
    /// <summary>
    /// Gắn <see cref="LogService"/> vào DataGrid: đổ dòng mới sang giao diện theo timer 33 ms (~30 lần/giây),
    /// tự cuộn xuống cuối, và xử lý nút Clear. Toàn bộ lớp này chạy trên luồng UI.
    /// <para>Tự cuộn kiểu "tail -f": chỉ cuộn xuống cuối khi người dùng đang ở cuối bảng.
    /// Cuộn lên xem dòng cũ thì bảng không bị giật xuống nữa.</para>
    /// </summary>
    public sealed class LogPanel
    {
        private readonly LogService log;
        private readonly DataGrid grid;
        private readonly TextBlock infoText;
        private readonly DispatcherTimer flushTimer;

        private ScrollViewer scroller;
        private string lastInfo;

        /// <param name="infoText">Dòng chữ nhỏ hiện số dòng, có thể null.</param>
        public LogPanel(LogService log, DataGrid grid, Button clearButton, TextBlock infoText)
        {
            this.log = log;
            this.grid = grid;
            this.infoText = infoText;

            grid.ItemsSource = log.View;
            clearButton.Click += (s, e) => Clear();
            grid.IsVisibleChanged += OnGridVisibleChanged;

            UpdateInfo();

            flushTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            flushTimer.Tick += OnTick;
            flushTimer.Start();
        }

        /// <summary>Xoá log (cả dữ liệu lưu phía sau, không chỉ phần đang hiển thị).</summary>
        public void Clear()
        {
            log.Clear();
            UpdateInfo();
        }

        private void OnTick(object sender, EventArgs e)
        {
            bool follow = IsAtBottom();   // phải đo trước khi thêm dòng mới
            if (!log.Flush()) return;

            UpdateInfo();
            if (follow) ScrollToEnd();
        }

        // Card Log thu gọn thì DataGrid không hiển thị; mở ra lần đầu thì nhảy xuống dòng mới nhất.
        private void OnGridVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (grid.IsVisible)
                grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollToEnd));
        }

        private void ScrollToEnd()
        {
            if (!grid.IsVisible || grid.Items.Count == 0) return;
            grid.ScrollIntoView(grid.Items[grid.Items.Count - 1]);
        }

        private bool IsAtBottom()
        {
            if (scroller == null) scroller = FindScrollViewer(grid);
            if (scroller == null) return true;   // chưa dựng xong giao diện: coi như đang ở cuối
            return scroller.VerticalOffset >= scroller.ScrollableHeight - 1.0;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                ScrollViewer sv = child as ScrollViewer;
                if (sv != null) return sv;

                sv = FindScrollViewer(child);
                if (sv != null) return sv;
            }
            return null;
        }

        private void UpdateInfo()
        {
            if (infoText == null) return;

            int total = log.StoredCount;
            int shown = log.View.Count;

            string text;
            if (total == 0) text = "No log yet.";
            else if (shown < total)
                text = string.Format(CultureInfo.InvariantCulture, "Showing last {0:N0} of {1:N0} lines", shown, total);
            else
                text = string.Format(CultureInfo.InvariantCulture, "{0:N0} lines", total);

            if (text == lastInfo) return;
            lastInfo = text;
            infoText.Text = text;
        }
    }
}
