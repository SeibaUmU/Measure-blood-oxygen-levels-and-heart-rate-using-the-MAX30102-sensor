using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MAX30102_WPF.Measurement;

namespace MAX30102_WPF.Reporting
{
    /// <summary>Xuất báo cáo đo (thống kê BPM / SpO2 và dữ liệu sóng PPG) ra file CSV.</summary>
    public static class ReportExporter
    {
        /// <summary>Tên file gợi ý: PPG_Report_yyyyMMdd_HHmmss.csv</summary>
        public static string DefaultFileName()
        {
            return "PPG_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
        }

        public static void WriteReport(string path, RunningStat bpm, RunningStat spo2,
                                       long firstIndex, IEnumerable<double> samples)
        {
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("=== MAX30102 HEALTH MONITOR REPORT ===");
                sw.WriteLine("Export Time," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sw.WriteLine("BPM Average," + (bpm.HasData ? bpm.Avg.ToString("0.0", CultureInfo.InvariantCulture) : "--"));
                sw.WriteLine("BPM Min / Max," + (bpm.HasData ? bpm.Min + " / " + bpm.Max : "-- / --"));
                sw.WriteLine("SpO2 Average," + (spo2.HasData ? spo2.Avg.ToString("0.0", CultureInfo.InvariantCulture) : "--"));
                sw.WriteLine("SpO2 Min / Max," + (spo2.HasData ? spo2.Min + " / " + spo2.Max : "-- / --"));
                sw.WriteLine();

                sw.WriteLine("=== PPG WAVEFORM DATA ===");
                sw.WriteLine("Sample_Index,PPG_Amplitude");

                // InvariantCulture: tránh máy để locale VN ghi dấu phẩy thập phân làm vỡ CSV
                long idx = firstIndex;
                foreach (double v in samples)
                {
                    sw.WriteLine(idx + "," + v.ToString(CultureInfo.InvariantCulture));
                    idx++;
                }
            }
        }
    }
}
