using System;
using System.Collections.Generic;

namespace MAX30102_WPF.Protocol
{
    /// <summary>
    /// Ghép luồng byte thô từ cổng COM thành các <see cref="DataFrame"/> hoàn chỉnh.
    /// <para>Khác bản cũ: khi một khung sai footer / CRC, parser chỉ bỏ 1 byte (byte header giả)
    /// rồi quét tiếp, thay vì bỏ cả độ dài khung. Nhờ vậy 1 byte hỏng không làm mất thêm gói kế tiếp.
    /// Khung hỏng chỉ được đếm (<see cref="BadFrameCount"/>), không có sự kiện riêng.</para>
    /// <para><see cref="FrameParsed"/> được gọi từ luồng đang gọi <see cref="Feed"/> (luồng của SerialPort),
    /// không phải luồng UI. Sự kiện được phát ngoài lock, nên handler chậm hoặc đọc bộ đếm cũng không làm kẹt parser.</para>
    /// </summary>
    public sealed class FrameParser
    {
        private readonly List<byte> buffer = new List<byte>();
        private readonly object sync = new object();
        private long badFrames;
        private long skippedBytes;

        /// <summary>Có một khung hợp lệ.</summary>
        public event Action<DataFrame> FrameParsed;

        /// <summary>Số khung bị loại vì sai footer / CRC.</summary>
        public long BadFrameCount { get { lock (sync) { return badFrames; } } }

        /// <summary>Số byte bị bỏ mà không thuộc khung hợp lệ nào.</summary>
        public long SkippedByteCount { get { lock (sync) { return skippedBytes; } } }

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
            List<DataFrame> frames = new List<DataFrame>();

            lock (sync)
            {
                for (int i = 0; i < count; i++) buffer.Add(data[i]);
                Extract(frames);
            }

            // Phát sự kiện ngoài lock, đúng thứ tự xuất hiện trên dây.
            foreach (DataFrame frame in frames) FrameParsed?.Invoke(frame);
        }

        private void Extract(List<DataFrame> frames)
        {
            while (buffer.Count >= ProtocolConst.MinBytesToParse)
            {
                // 1) Phải bắt đầu bằng header, và loại gói (byte thứ 2) quyết định độ dài khung
                int length = DataFrame.LengthOf(buffer[1]);
                if (buffer[0] != ProtocolConst.Header || length == 0)
                {
                    DropFirstByte();
                    continue;
                }

                if (buffer.Count < length) break;   // chưa đủ byte, chờ thêm

                // 2) Kiểm tra footer và CRC
                byte[] raw = buffer.GetRange(0, length).ToArray();
                if (DataFrame.IsValid(raw))
                {
                    buffer.RemoveRange(0, length);
                    frames.Add(new DataFrame(raw));
                }
                else
                {
                    // Chỉ bỏ 1 byte rồi quét lại: byte header của gói kế tiếp có thể nằm trong "khung" hỏng này.
                    badFrames++;
                    DropFirstByte();
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
