using System;

namespace MAX30102_WPF.Protocol
{
    /// <summary>Dựng các khung gửi từ PC xuống firmware.</summary>
    public static class FrameBuilder
    {
        /// <summary>CRC = XOR của <paramref name="count"/> byte bắt đầu từ vị trí <paramref name="start"/>.</summary>
        public static byte Crc(byte[] frame, int start, int count)
        {
            byte crc = 0;
            for (int i = start; i < start + count; i++) crc ^= frame[i];
            return crc;
        }

        /// <summary>Khung lệnh PC -> MCU: <c>[40][02][payload...][26][CRC]</c>.</summary>
        public static byte[] BuildCommand(byte[] payload)
        {
            int n = payload.Length;
            byte[] frame = new byte[4 + n];
            frame[0] = ProtocolConst.Header;
            frame[1] = ProtocolConst.TypeCommand;
            Array.Copy(payload, 0, frame, 2, n);
            frame[2 + n] = ProtocolConst.Footer;
            frame[3 + n] = Crc(frame, 1, n + 2);   // từ Type đến Footer
            return frame;
        }

        /// <summary>Khung lệnh một ký tự, ví dụ <see cref="ProtocolConst.CmdStart"/>.</summary>
        public static byte[] BuildCommand(byte command)
        {
            return BuildCommand(new byte[] { command });
        }

        /// <summary>Khung test (không CRC): <c>[40][04][b2][b3][26]</c>.</summary>
        public static byte[] BuildTest(byte b2, byte b3)
        {
            return new byte[] { ProtocolConst.Header, ProtocolConst.TypeTest, b2, b3, ProtocolConst.Footer };
        }

        /// <summary>Khung test mang số 16 bit (byte cao trước).</summary>
        public static byte[] BuildTest(ushort value)
        {
            return BuildTest((byte)(value >> 8), (byte)(value & 0xFF));
        }

        /// <summary>Khung điều khiển test: <c>[40][04][84][cmd][26]</c> (cmd = TestBegin / TestEnd).</summary>
        public static byte[] BuildTestControl(byte cmd)
        {
            return BuildTest(ProtocolConst.TestControlMarker, cmd);
        }
    }
}
