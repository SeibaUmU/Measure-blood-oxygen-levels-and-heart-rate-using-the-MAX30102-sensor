using System;

namespace MAX30102_WPF.Protocol
{
    /// <summary>
    /// Một khung dữ liệu của giao thức, dùng cho cả hai chiều: khung nhận từ firmware (đã qua kiểm tra
    /// footer, và CRC nếu có) lẫn khung PC dựng để gửi xuống. Bất biến: tạo xong không sửa được.
    /// <para>Lớp này giữ luôn quy tắc của khung: độ dài theo loại gói (<see cref="LengthOf"/>),
    /// kiểm tra hợp lệ (<see cref="IsValid"/>), CRC (<see cref="Crc"/>) và các hàm dựng khung gửi
    /// (<see cref="Command(byte)"/>, <see cref="Test(byte, byte)"/>, <see cref="TestControl"/>).
    /// Hằng số (header, footer, mã loại gói, mã lệnh) nằm ở <see cref="ProtocolConst"/>.</para>
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

        // =====================================================================
        //  QUY TẮC KHUNG (dùng cho khung nhận từ MCU)
        // =====================================================================

        /// <summary>Độ dài khung theo loại gói; 0 nếu loại gói không hợp lệ.</summary>
        public static int LengthOf(byte type)
        {
            switch (type)
            {
                case ProtocolConst.TypeVitals:
                case ProtocolConst.TypePpg:
                    return ProtocolConst.LengthWithCrc;
                case ProtocolConst.TypeTest:
                    return ProtocolConst.LengthWithoutCrc;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Kiểm tra footer và CRC (nếu có). <paramref name="raw"/> phải có đúng độ dài <see cref="LengthOf"/>
        /// của loại gói: khung 6 byte có CRC ở cuối, khung 5 byte thì không.
        /// </summary>
        public static bool IsValid(byte[] raw)
        {
            bool hasCrc = raw.Length == ProtocolConst.LengthWithCrc;
            int footerIndex = hasCrc ? raw.Length - 2 : raw.Length - 1;

            if (raw[footerIndex] != ProtocolConst.Footer) return false;
            return !hasCrc || Crc(raw, 1, raw.Length - 2) == raw[raw.Length - 1];   // CRC từ Type đến Footer
        }

        /// <summary>CRC = XOR của <paramref name="count"/> byte bắt đầu từ vị trí <paramref name="start"/>.</summary>
        public static byte Crc(byte[] frame, int start, int count)
        {
            byte crc = 0;
            for (int i = start; i < start + count; i++) crc ^= frame[i];
            return crc;
        }

        // =====================================================================
        //  DỰNG KHUNG GỬI TỪ PC XUỐNG FIRMWARE
        // =====================================================================

        /// <summary>Khung lệnh PC -> MCU: <c>[40][02][payload...][26][CRC]</c> (payload phải có ít nhất 1 byte).</summary>
        public static DataFrame Command(byte[] payload)
        {
            int n = payload.Length;
            byte[] frame = new byte[4 + n];
            frame[0] = ProtocolConst.Header;
            frame[1] = ProtocolConst.TypeCommand;
            Array.Copy(payload, 0, frame, 2, n);
            frame[2 + n] = ProtocolConst.Footer;
            frame[3 + n] = Crc(frame, 1, n + 2);   // từ Type đến Footer
            return new DataFrame(frame);
        }

        /// <summary>Khung lệnh một ký tự, ví dụ <see cref="ProtocolConst.CmdStart"/>.</summary>
        public static DataFrame Command(byte command)
        {
            return Command(new byte[] { command });
        }

        /// <summary>Khung test (không CRC): <c>[40][04][b2][b3][26]</c>.</summary>
        public static DataFrame Test(byte b2, byte b3)
        {
            return new DataFrame(new byte[] { ProtocolConst.Header, ProtocolConst.TypeTest, b2, b3, ProtocolConst.Footer });
        }

        /// <summary>Khung test mang số 16 bit (byte cao trước).</summary>
        public static DataFrame Test(ushort value)
        {
            return Test((byte)(value >> 8), (byte)(value & 0xFF));
        }

        /// <summary>Khung điều khiển test: <c>[40][04][84][cmd][26]</c> (cmd = TestBegin / TestEnd).</summary>
        public static DataFrame TestControl(byte cmd)
        {
            return Test(ProtocolConst.TestControlMarker, cmd);
        }
    }
}
