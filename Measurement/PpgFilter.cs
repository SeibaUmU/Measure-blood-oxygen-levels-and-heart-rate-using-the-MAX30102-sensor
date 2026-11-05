namespace MAX30102_WPF.Measurement
{
    /// <summary>
    /// Tách thành phần DC khỏi mẫu PPG thô bằng trung bình trượt chậm, rồi đảo dấu
    /// để đỉnh sóng trên biểu đồ ứng với nhịp tim.
    /// </summary>
    public sealed class PpgFilter
    {
        private const double DcSmoothing = 0.05;   // hệ số trung bình trượt (càng nhỏ càng chậm)
        private double dc;

        /// <summary>Nhận mẫu thô 16 bit, trả về giá trị AC đã đảo dấu để vẽ.</summary>
        public double Process(ushort raw)
        {
            if (dc == 0) dc = raw;
            dc += (raw - dc) * DcSmoothing;   // trung bình trượt chậm = thành phần DC
            return dc - raw;                  // đảo dấu: đỉnh sóng ứng với nhịp tim
        }

        public void Reset()
        {
            dc = 0;
        }
    }
}
