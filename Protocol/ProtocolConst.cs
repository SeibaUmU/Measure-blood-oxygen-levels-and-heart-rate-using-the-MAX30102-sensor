namespace MAX30102_WPF.Protocol
{
    /// <summary>
    /// Hằng số của giao thức khung dữ liệu giữa PC và firmware.
    /// <para>Khung có CRC (type 0x01, 0x03 từ MCU gửi lên; type 0x02 từ PC gửi xuống):
    /// <c>[40][Type][Data...][26][CRC]</c>, trong đó CRC = XOR từ byte Type đến byte Footer.</para>
    /// <para>Khung test (type 0x04), không có CRC: <c>[40][04][b2][b3][26]</c>.</para>
    /// </summary>
    public static class ProtocolConst
    {
        // ----- Đầu / cuối khung -----
        public const byte Header = 0x40;   // '@'
        public const byte Footer = 0x26;   // '&'

        // ----- Loại gói (byte TypeData, vị trí thứ 2 trong khung) -----
        public const byte TypeVitals = 0x01;    // MCU -> PC: [BPM][SpO2]
        public const byte TypeCommand = 0x02;   // PC -> MCU: lệnh điều khiển
        public const byte TypePpg = 0x03;       // MCU -> PC: mẫu sóng PPG thô (16 bit)
        public const byte TypeTest = 0x04;      // hai chiều: gói test loopback

        // ----- Lệnh PC -> MCU (nằm trong gói TypeCommand) -----
        public const byte CmdStart = (byte)'1';
        public const byte CmdStop = (byte)'0';
        public const byte CmdReset = (byte)'R';

        // ----- Mã lỗi cảm biến do firmware báo (ở byte BPM hoặc SpO2) -----
        public const byte SensorError = 0xFF;

        // ----- Gói điều khiển test: [40][04][0x84][cmd][26] -----
        // Lưu ý: dữ liệu test do PC sinh ra KHÔNG được trùng cặp (0x84, 0x83) hoặc (0x84, 0x69),
        // nếu không firmware sẽ hiểu nhầm là lệnh bắt đầu / kết thúc test.
        public const byte TestControlMarker = 0x84;
        public const byte TestBegin = 0x83;
        public const byte TestEnd = 0x69;

        // ----- Độ dài khung -----
        public const int LengthWithCrc = 6;      // 0x01, 0x03
        public const int LengthWithoutCrc = 5;   // 0x04

        /// <summary>Số byte tối thiểu trong bộ đệm trước khi parser thử đọc khung.</summary>
        public const int MinBytesToParse = 4;
    }
}
