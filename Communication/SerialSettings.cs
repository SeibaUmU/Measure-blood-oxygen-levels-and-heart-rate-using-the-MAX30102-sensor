using System.IO.Ports;

namespace MAX30102_WPF.Communication
{
    /// <summary>
    /// Cấu hình cổng COM. Mặc định 9600-8-N-1 (9600 baud, 8 data bit, không parity, 1 stop bit),
    /// trùng với mặc định .NET đang dùng ngầm trước đây.
    /// </summary>
    public sealed class SerialSettings
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public Parity Parity { get; set; } = Parity.None;
        public StopBits StopBits { get; set; } = StopBits.One;
    }
}
