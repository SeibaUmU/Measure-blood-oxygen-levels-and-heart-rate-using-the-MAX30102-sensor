using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MAX30102_WPF.Communication;

namespace MAX30102_WPF.Logging
{
    /// <summary>
    /// Một dòng log: thời điểm, hướng (Send / Receive), khung dạng hex và trạng thái.
    /// Cột Status chưa dùng nên luôn để trống; <see cref="Time"/> chưa hiển thị nhưng được giữ lại cho file tải về sau này.
    /// </summary>
    public sealed class LogEntry
    {
        public LogEntry(DateTime time, string direction, string data)
        {
            Time = time;
            Direction = direction;
            Data = data;
            Status = string.Empty;
        }

        public DateTime Time { get; private set; }

        /// <summary>"Send" hoặc "Receive" (xem <see cref="LogService.Send"/>, <see cref="LogService.Receive"/>).</summary>
        public string Direction { get; private set; }

        /// <summary>Khung dạng hex, ví dụ "40-01-48-62-26-4B".</summary>
        public string Data { get; private set; }

        public string Status { get; private set; }
    }

    /// <summary>
    /// Log kiểu FIFO của cổng COM: mỗi khung gửi / nhận là một dòng, dòng mới nằm cuối.
    /// <para>Phía ghi an toàn đa luồng: <see cref="AddSend"/> / <see cref="AddReceive"/> gọi từ luồng nào cũng được
    /// (luồng SerialPort, luồng UI...) vì chỉ đẩy vào hàng đợi. <see cref="Flush"/>, <see cref="Clear"/>,
    /// <see cref="View"/> và <see cref="StoredCount"/> chỉ dùng trên luồng UI (LogPanel gọi Flush theo timer 33 ms).</para>
    /// <para>Dữ liệu nằm ở hai nơi: danh sách đầy đủ (tối đa <c>MaxStored</c> dòng, để sau này làm nút Tải)
    /// và <see cref="View"/> chỉ giữ <see cref="DisplayLimit"/> dòng cuối cho DataGrid, tránh giao diện nặng khi log dài.</para>
    /// </summary>
    public sealed class LogService
    {
        public const string Send = "Send";
        public const string Receive = "Receive";

        /// <summary>Số dòng cuối được hiển thị trên giao diện.</summary>
        public const int DisplayLimit = 500;

        private const int MaxStored = 200000;   // quá số này thì bỏ bớt 10% dòng cũ nhất
        private const int MaxPerFlush = 2000;   // mỗi lần Flush lấy tối đa bấy nhiêu dòng, phần dư để lần sau

        private readonly ConcurrentQueue<LogEntry> pending = new ConcurrentQueue<LogEntry>();
        private readonly List<LogEntry> all = new List<LogEntry>();
        private readonly ObservableCollection<LogEntry> view = new ObservableCollection<LogEntry>();

        /// <summary>Danh sách các dòng cuối cùng để gắn vào DataGrid (chỉ đụng tới trên luồng UI).</summary>
        public ObservableCollection<LogEntry> View { get { return view; } }

        /// <summary>Tổng số dòng đang lưu (đã Flush), lớn hơn hoặc bằng số dòng đang hiển thị.</summary>
        public int StoredCount { get { return all.Count; } }

        /// <summary>
        /// Tự ghi log mọi khung gửi / nhận của <paramref name="serial"/>.
        /// Gửi lấy từ <see cref="SerialService.FrameSent"/> (một lần cho mỗi khung, không trùng với sự kiện FrameSent của test),
        /// nhận lấy từ <see cref="SerialService.FrameReceived"/> (chỉ khung hợp lệ).
        /// </summary>
        public void Attach(SerialService serial)
        {
            if (serial == null) throw new ArgumentNullException("serial");
            serial.FrameSent += frame => AddSend(frame);
            serial.FrameReceived += frame => AddReceive(frame.Raw);
        }

        public void AddSend(byte[] frame)
        {
            Enqueue(Send, frame);
        }

        public void AddReceive(byte[] frame)
        {
            Enqueue(Receive, frame);
        }

        private void Enqueue(string direction, byte[] frame)
        {
            if (frame == null || frame.Length == 0) return;
            pending.Enqueue(new LogEntry(DateTime.Now, direction, BitConverter.ToString(frame)));
        }

        /// <summary>
        /// Chuyển các dòng đang chờ vào danh sách đầy đủ và vào <see cref="View"/>.
        /// Trả về true nếu có dòng mới. Chỉ gọi trên luồng UI.
        /// </summary>
        public bool Flush()
        {
            List<LogEntry> batch = null;
            LogEntry entry;
            while (batch == null || batch.Count < MaxPerFlush)
            {
                if (!pending.TryDequeue(out entry)) break;
                if (batch == null) batch = new List<LogEntry>();
                batch.Add(entry);
            }
            if (batch == null) return false;

            all.AddRange(batch);
            if (all.Count > MaxStored) all.RemoveRange(0, all.Count - (MaxStored - MaxStored / 10));

            // Dòng nào chắc chắn bị đẩy ra khỏi cửa sổ hiển thị thì khỏi thêm vào View
            int skip = Math.Max(0, batch.Count - DisplayLimit);
            for (int i = skip; i < batch.Count; i++) view.Add(batch[i]);
            while (view.Count > DisplayLimit) view.RemoveAt(0);

            return true;
        }

        /// <summary>Xoá toàn bộ log (cả dòng đang chờ, danh sách đầy đủ và phần hiển thị). Chỉ gọi trên luồng UI.</summary>
        public void Clear()
        {
            LogEntry dummy;
            while (pending.TryDequeue(out dummy)) { }
            all.Clear();
            view.Clear();
        }
    }
}
