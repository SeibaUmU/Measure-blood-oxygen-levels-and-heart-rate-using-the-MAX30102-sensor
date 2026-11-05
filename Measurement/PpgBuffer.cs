using System.Collections.Generic;

namespace MAX30102_WPF.Measurement
{
    /// <summary>
    /// Bộ đệm cửa sổ trượt chứa các mẫu PPG mới nhất. <see cref="FirstIndex"/> là số thứ tự tuyệt đối
    /// của mẫu đầu tiên đang giữ, để biểu đồ và file xuất dùng cùng một trục mẫu.
    /// </summary>
    public sealed class PpgBuffer
    {
        private readonly List<double> samples = new List<double>();

        public PpgBuffer(int windowSize)
        {
            WindowSize = windowSize;
        }

        public int WindowSize { get; private set; }

        /// <summary>Số thứ tự tuyệt đối của mẫu đầu tiên trong bộ đệm.</summary>
        public long FirstIndex { get; private set; }

        public int Count { get { return samples.Count; } }

        public void Add(double value)
        {
            samples.Add(value);
            if (samples.Count > WindowSize)
            {
                samples.RemoveAt(0);
                FirstIndex++;
            }
        }

        public void Clear()
        {
            samples.Clear();
            FirstIndex = 0;
        }

        public double[] ToArray()
        {
            return samples.ToArray();
        }
    }
}
