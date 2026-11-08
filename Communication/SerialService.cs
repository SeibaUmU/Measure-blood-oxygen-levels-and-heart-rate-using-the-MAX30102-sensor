using System;
using System.IO.Ports;
using System.Linq;
using MAX30102_WPF.Protocol;

namespace MAX30102_WPF.Communication
{
    /// <summary>
    /// Bao toàn bộ việc làm việc với cổng COM: mở / đóng, gửi khung, nhận byte và tách khung.
    /// MainWindow chỉ gọi lớp này, không đụng trực tiếp tới <see cref="SerialPort"/>.
    /// <para><see cref="FrameReceived"/> được gọi từ luồng của SerialPort, phải dùng Dispatcher khi cập nhật giao diện.
    /// <see cref="FrameSent"/> được gọi ngay trên luồng gọi <see cref="Send"/>.</para>
    /// </summary>
    public sealed class SerialService : IDisposable
    {
        private readonly SerialPort port = new SerialPort();
        private readonly FrameParser parser = new FrameParser();

        public SerialService()
        {
            port.DataReceived += OnDataReceived;   // đăng ký một lần; đóng cổng là sự kiện tự dừng
            parser.FrameParsed += frame => FrameReceived?.Invoke(frame);
        }

        /// <summary>Nhận được một khung hợp lệ.</summary>
        public event Action<DataFrame> FrameReceived;

        /// <summary>Vừa gửi xong một khung.</summary>
        public event Action<DataFrame> FrameSent;

        public bool IsOpen { get { return port.IsOpen; } }

        /// <summary>Số khung bị loại vì sai footer / CRC từ lúc mở cổng.</summary>
        public long BadFrameCount { get { return parser.BadFrameCount; } }

        /// <summary>Danh sách cổng COM hiện có, sắp theo độ dài tên rồi theo bảng chữ cái (COM2 trước COM10).</summary>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames()
                             .OrderBy(p => p.Length)
                             .ThenBy(p => p)
                             .ToArray();
        }

        /// <summary>Mở cổng với cấu hình đã cho. Ném ngoại lệ nếu không mở được.</summary>
        public void Open(SerialSettings settings)
        {
            if (port.IsOpen) throw new InvalidOperationException("Cổng COM đang được mở rồi!");

            port.PortName = settings.PortName;
            port.BaudRate = settings.BaudRate;
            port.DataBits = settings.DataBits;
            port.Parity = settings.Parity;
            port.StopBits = settings.StopBits;

            // Bỏ qua tín hiệu bắt tay phần cứng (tránh lỗi driver CH340)
            port.DtrEnable = false;
            port.RtsEnable = false;

            parser.Reset();   // bỏ byte dư của lần kết nối trước
            port.Open();
        }

        /// <summary>Đóng cổng (không làm gì nếu cổng đang đóng).</summary>
        public void Close()
        {
            if (port.IsOpen) port.Close();
        }

        /// <summary>Gửi nguyên khung đã dựng sẵn (xem các hàm dựng khung trong <see cref="DataFrame"/>).</summary>
        public void Send(DataFrame frame)
        {
            if (!port.IsOpen) throw new InvalidOperationException("Cổng COM chưa được mở.");

            port.Write(frame.Raw, 0, frame.Raw.Length);
            FrameSent?.Invoke(frame);
        }

        public void Dispose()
        {
            try { Close(); } catch (Exception) { }
            port.Dispose();
        }

        // Chạy trên luồng thread-pool của SerialPort
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int toRead = port.BytesToRead;
                if (toRead <= 0) return;

                byte[] temp = new byte[toRead];
                int read = port.Read(temp, 0, toRead);
                parser.Feed(temp, read);
            }
            catch (Exception)
            {
                // Bỏ qua ngoại lệ khi ngắt kết nối đột ngột
            }
        }
    }
}
