using System;

namespace MAX30102_WPF.Protocol
{
    /// <summary>
    /// Một khung dữ liệu hợp lệ nhận từ firmware (đã qua kiểm tra footer, và CRC nếu có).
    /// Bất biến: tạo xong không sửa được.
    /// </summary>
    public sealed class DataFrame
    {
        public DataFrame(byte[] raw)
        {
            if (raw == null) throw new ArgumentNullException("raw");
            if (raw.Length < ProtocolConst.LengthWithoutCrc) throw new ArgumentException("Khung quá ngắn.", "raw");
            Raw = raw;
        }

        /// <summary>Toàn bộ byte của khung, gồm header, footer (và CRC nếu có).</summary>
        public byte[] Raw { get; private set; }

        /// <summary>Loại gói (<see cref="ProtocolConst.TypeVitals"/>, TypePpg, TypeTest...).</summary>
        public byte Type { get { return Raw[1]; } }

        /// <summary>Byte dữ liệu thứ nhất (BPM, hoặc byte cao của PPG / gói test).</summary>
        public byte Data0 { get { return Raw[2]; } }

        /// <summary>Byte dữ liệu thứ hai (SpO2, hoặc byte thấp của PPG / gói test).</summary>
        public byte Data1 { get { return Raw[3]; } }

        /// <summary>Hai byte dữ liệu ghép thành số 16 bit: (Data0 &lt;&lt; 8) | Data1.</summary>
        public ushort Word { get { return (ushort)((Raw[2] << 8) | Raw[3]); } }

        /// <summary>Chuỗi hex dạng "40-01-48-62-26-4B", dùng để hiển thị / ghi log.</summary>
        public string Hex { get { return BitConverter.ToString(Raw); } }
    }
}
