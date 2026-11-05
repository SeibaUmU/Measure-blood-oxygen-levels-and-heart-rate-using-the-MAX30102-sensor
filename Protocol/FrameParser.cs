using System;
using System.Collections.Generic;

namespace MAX30102_WPF.Protocol
{
    /// <summary>Lý do một khung bị loại.</summary>
    public enum RejectReason
    {
        BadFooter,
        BadCrc
    }

    /// <summary>
    /// Ghép luồng byte thô từ cổng COM thành các <see cref="DataFrame"/> hoàn chỉnh.
    /// <para>Khác bản cũ: khi một khung sai footer / CRC, parser chỉ bỏ 1 byte (byte header giả)
    /// rồi quét tiếp, thay vì bỏ cả độ dài khung. Nhờ vậy 1 byte hỏng không làm mất thêm gói kế tiếp.</para>
    /// <para>Các sự kiện được gọi từ luồng đang gọi <see cref="Feed"/> (luồng của SerialPort),
    /// không phải luồng UI.</para>
    /// </summary>
    public sealed class FrameParser
    {
        private sealed class ParseItem
        {
            public DataFrame Frame;        // khác null: khung hợp lệ
            public byte[] RejectedRaw;     // khác null: khung bị loại
            public RejectReason Reason;
        }

        private readonly List<byte> buffer = new List<byte>();
        private readonly object sync = new object();
        private long badFrames;
        private long skippedBytes;

        /// <summary>Có một khung hợp lệ.</summary>
        public event Action<DataFrame> FrameParsed;

        /// <summary>Có một khung đủ độ dài nhưng sai footer hoặc CRC (byte thô, lý do).</summary>
        public event Action<byte[], RejectReason> FrameRejected;

        /// <summary>Số khung bị loại vì sai footer / CRC.</summary>
        public long BadFrameCount { get { lock (sync) { return badFrames; } } }

        /// <summary>Số byte bị bỏ mà không thuộc khung hợp lệ nào.</summary>
        public long SkippedByteCount { get { lock (sync) { return skippedBytes; } } }

        /// <summary>Cho biết độ dài khung và có CRC hay không theo loại gói.</summary>
        public static bool TryGetFrameLength(byte type, out int length, out bool hasCrc)
        {
            if (type == ProtocolConst.TypeVitals || type == ProtocolConst.TypePpg)
            {
                length = ProtocolConst.LengthWithCrc;
                hasCrc = true;
                return true;
            }
            if (type == ProtocolConst.TypeTest)
            {
                length = ProtocolConst.LengthWithoutCrc;
                hasCrc = false;
                return true;
            }
            length = 0;
            hasCrc = false;
            return false;
        }

        /// <summary>Xoá bộ đệm và bộ đếm (gọi khi mở cổng mới).</summary>
        public void Reset()
        {
            lock (sync)
            {
                buffer.Clear();
                badFrames = 0;
                skippedBytes = 0;
            }
        }

        /// <summary>Nạp thêm byte vừa nhận và phát sự kiện cho từng khung tách được.</summary>
        public void Feed(byte[] data, int count)
        {
            List<ParseItem> items = new List<ParseItem>();

            lock (sync)
            {
                for (int i = 0; i < count; i++) buffer.Add(data[i]);
                Extract(items);
            }

            // Phát sự kiện ngoài lock, đúng thứ tự xuất hiện trên dây.
            foreach (ParseItem item in items)
            {
                if (item.Frame != null)
                {
                    Action<DataFrame> ok = FrameParsed;
                    if (ok != null) ok(item.Frame);
                }
                else
                {
                    Action<byte[], RejectReason> bad = FrameRejected;
                    if (bad != null) bad(item.RejectedRaw, item.Reason);
                }
            }
        }

        private void Extract(List<ParseItem> items)
        {
            while (buffer.Count >= ProtocolConst.MinBytesToParse)
            {
                // 1) Tìm header
                if (buffer[0] != ProtocolConst.Header)
                {
                    DropFirstByte();
                    continue;
                }

                // 2) Loại gói quyết định độ dài khung
                int length;
                bool hasCrc;
                if (!TryGetFrameLength(buffer[1], out length, out hasCrc))
                {
                    DropFirstByte();
                    continue;
                }

                if (buffer.Count < length) break;   // chưa đủ byte, chờ thêm

                byte[] raw = buffer.GetRange(0, length).ToArray();

                // 3) Kiểm tra footer và CRC
                int footerIndex = hasCrc ? length - 2 : length - 1;
                bool valid = raw[footerIndex] == ProtocolConst.Footer;
                RejectReason reason = RejectReason.BadFooter;

                if (valid && hasCrc)
                {
                    byte expected = FrameBuilder.Crc(raw, 1, length - 2);   // từ Type đến Footer
                    if (expected != raw[length - 1])
                    {
                        valid = false;
                        reason = RejectReason.BadCrc;
                    }
                }

                if (valid)
                {
                    buffer.RemoveRange(0, length);
                    items.Add(new ParseItem { Frame = new DataFrame(raw) });
                }
                else
                {
                    // Chỉ bỏ 1 byte rồi quét lại: byte header của gói kế tiếp có thể nằm trong "khung" hỏng này.
                    badFrames++;
                    DropFirstByte();
                    items.Add(new ParseItem { RejectedRaw = raw, Reason = reason });
                }
            }
        }

        private void DropFirstByte()
        {
            buffer.RemoveAt(0);
            skippedBytes++;
        }
    }
}
