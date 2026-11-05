namespace MAX30102_WPF.Measurement
{
    /// <summary>
    /// Thống kê chạy (running statistics): nhỏ nhất, lớn nhất, trung bình của một chuỗi số nguyên
    /// mà không cần lưu lại toàn bộ giá trị. Giá trị &lt;= 0 bị bỏ qua (coi là chưa đo được).
    /// </summary>
    public sealed class RunningStat
    {
        private long sum;
        private int count;

        public int Min { get; private set; }
        public int Max { get; private set; }
        public bool HasData { get { return count > 0; } }
        public double Avg { get { return count > 0 ? (double)sum / count : 0; } }

        public void Add(int value)
        {
            if (value <= 0) return;
            if (count == 0 || value < Min) Min = value;
            if (count == 0 || value > Max) Max = value;
            sum += value;
            count++;
        }

        public void Clear()
        {
            sum = 0;
            count = 0;
            Min = 0;
            Max = 0;
        }
    }
}
