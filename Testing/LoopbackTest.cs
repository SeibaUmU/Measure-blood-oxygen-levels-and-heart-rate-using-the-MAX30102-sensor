using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MAX30102_WPF.Communication;
using MAX30102_WPF.Protocol;

namespace MAX30102_WPF.Testing
{
    /// <summary>Kết quả một lần chạy test loopback.</summary>
    public sealed class LoopbackTestResult
    {
        public LoopbackTestResult(int successCount, int total, IList<int> lostIndices)
        {
            SuccessCount = successCount;
            Total = total;
            LostIndices = lostIndices;
        }

        public int SuccessCount { get; private set; }
        public int Total { get; private set; }

        /// <summary>Danh sách số thứ tự gói không nhận lại được.</summary>
        public IList<int> LostIndices { get; private set; }

        public bool IsPerfect { get { return SuccessCount == Total; } }
    }

    /// <summary>
    /// Test loopback hiện tại: gửi 1000 gói test mang số 1..1000, firmware echo lại nguyên gói,
    /// PC kiểm tra từng số có quay về đúng một lần hay không.
    /// <para>Lớp này chạy trên luồng UI: <see cref="RunAsync"/> phải được gọi từ luồng UI và
    /// <see cref="HandleFrame"/> cũng phải được gọi trên luồng UI (qua Dispatcher).</para>
    /// </summary>
    public sealed class  LoopbackTest
    {
        public const int PacketCount = 1000;

        private const int StartDelayMs = 50;      // chờ firmware vào chế độ test
        private const int SendIntervalMs = 30;    // khoảng cách giữa hai gói
        private const int TimeoutMs = 40000;      // chờ tối đa các gói quay về
        private const int PollIntervalMs = 100;

        private readonly SerialService serial;
        private readonly bool[] received = new bool[PacketCount + 1];   // chỉ số 1..PacketCount
        private int successCount;

        public LoopbackTest(SerialService serial)
        {
            this.serial = serial;
        }

        public bool IsRunning { get; private set; }

        /// <summary>Số gói đã gửi (1..PacketCount), để cập nhật thanh tiến trình.</summary>
        public event Action<int> ProgressChanged;

        /// <summary>Đưa vào mọi khung nhận được; chỉ khung test lúc đang chạy mới được tính.</summary>
        public void HandleFrame(DataFrame frame)
        {
            if (!IsRunning || frame.Type != ProtocolConst.TypeTest) return;

            ushort d = frame.Word;
            if (d >= 1 && d <= PacketCount && !received[d])
            {
                received[d] = true;
                successCount++;
            }
        }

        public async Task<LoopbackTestResult> RunAsync()
        {
            IsRunning = true;
            successCount = 0;
            Array.Clear(received, 0, received.Length);

            try
            {
                SendIfOpen(DataFrame.TestControl(ProtocolConst.TestBegin));
                await Task.Delay(StartDelayMs);

                for (int i = 1; i <= PacketCount; i++)
                {
                    if (!SendIfOpen(DataFrame.Test((ushort)i))) break;   // cổng đã đóng giữa chừng

                    Action<int> progress = ProgressChanged;
                    if (progress != null) progress(i);

                    await Task.Delay(SendIntervalMs);
                }


                int elapsedMs = 0;
                while (elapsedMs < TimeoutMs && successCount < PacketCount)
                {
                    await Task.Delay(PollIntervalMs);
                    elapsedMs += PollIntervalMs;
                }

                SendIfOpen(DataFrame.TestControl(ProtocolConst.TestEnd));

                List<int> lost = new List<int>();
                for (int i = 1; i <= PacketCount; i++)
                {
                    if (!received[i]) lost.Add(i);
                }
                return new LoopbackTestResult(successCount, PacketCount, lost);
            }
            finally
            {
                IsRunning = false;
            }
        }

        // Cổng đã đóng giữa chừng thì bỏ qua im lặng (giữ đúng hành vi bản cũ); trả về false nếu không gửi.
        private bool SendIfOpen(DataFrame frame)
        {
            if (!serial.IsOpen) return false;

            serial.Send(frame);
            return true;
        }
    }
}
