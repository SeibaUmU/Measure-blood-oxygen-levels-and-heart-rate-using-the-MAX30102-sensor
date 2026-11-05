using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MAX30102_WPF.Communication;
using MAX30102_WPF.Measurement;
using MAX30102_WPF.Protocol;
using MAX30102_WPF.Reporting;
using MAX30102_WPF.Testing;
using MAX30102_WPF.Ui;

namespace MAX30102_WPF
{
    /// <summary>
    /// Cửa sổ chính: chỉ chứa phần giao diện (xử lý nút bấm, cập nhật chữ / màu).
    /// Mọi việc khác được giao cho các module:
    ///   Communication (cổng COM), Protocol (khung dữ liệu), Measurement (thống kê / lọc PPG),
    ///   Reporting (xuất file), Testing (test loopback), Ui (biểu đồ, trái tim đập).
    /// </summary>
    public partial class MainWindow : Window
    {
        // ===== MODULE =====
        private readonly SerialService serial = new SerialService();
        private readonly PpgFilter ppgFilter = new PpgFilter();
        private readonly RunningStat bpmStat = new RunningStat();
        private readonly RunningStat spo2Stat = new RunningStat();
        private readonly LoopbackTest loopbackTest;
        private readonly HeartBeatAnimator heartAnimator;
        private readonly PpgChart ppgChart;

        // ===== START / STOP =====
        private bool isMeasuring;
        private static readonly Brush StartBrush = Hex("#1FA463");
        private static readonly Brush StopBrush = Hex("#D63A55");

        private static Brush Hex(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        public MainWindow()
        {
            InitializeComponent();

            serial.FrameReceived += OnFrameReceived;

            loopbackTest = new LoopbackTest(serial);
            loopbackTest.ProgressChanged += UpdateTestProgress;
            loopbackTest.FrameSent += frame => txtSend.Text = BitConverter.ToString(frame);

            heartAnimator = new HeartBeatAnimator(heartScale);
            ppgChart = new PpgChart(PpgPlot, txtHover, txtPins, chkFreeze);

            InitComboBoxes();
            SetupUIInitialState();
            ClearStats();
            UpdateStartStopStyle();
            btnStartStop.IsEnabledChanged += (s, e) => UpdateStartStopStyle();

            Closing += (s, e) => serial.Dispose();
        }

        // =====================================================================
        //  START / STOP
        // =====================================================================
        private void SetMeasuring(bool measuring)
        {
            isMeasuring = measuring;
            UpdateStartStopStyle();
        }

        private void UpdateStartStopStyle()
        {
            btnStartStop.Content = isMeasuring ? "Stop" : "Start";
            if (btnStartStop.IsEnabled)
            {
                btnStartStop.Background = isMeasuring ? StopBrush : StartBrush;
                btnStartStop.Foreground = Brushes.White;
            }
            else
            {
                btnStartStop.ClearValue(Control.BackgroundProperty);
                btnStartStop.ClearValue(Control.ForegroundProperty);
            }
        }

        // =====================================================================
        //  BPM / SpO2 STATS
        // =====================================================================
        private void ShowStats()
        {
            txtBpmStat.Text = FormatStat(bpmStat, "");
            txtSpO2Stat.Text = FormatStat(spo2Stat, "%");
        }

        private static string FormatStat(RunningStat s, string unit)
        {
            if (!s.HasData) return "Avg --\nMin --\nMax --";
            return "Avg " + s.Avg.ToString("0.0", CultureInfo.InvariantCulture) + unit +
                   "\nMin " + s.Min + unit +
                   "\nMax " + s.Max + unit;
        }

        private void ClearStats()
        {
            bpmStat.Clear();
            spo2Stat.Clear();
            ShowStats();
        }

        // =====================================================================
        //  PPG CHART (logic nằm trong Ui/PpgChart.cs)
        // =====================================================================
        private void ClearChartData()
        {
            ppgFilter.Reset();
            ppgChart.Clear();
        }

        private void chkFreeze_Changed(object sender, RoutedEventArgs e)
        {
            if (ppgChart != null) ppgChart.OnFreezeChanged();
        }

        private void btnClearPins_Click(object sender, RoutedEventArgs e)
        {
            ppgChart.ClearPins();
        }

        // =====================================================================
        //  COM PORT & BAUD RATE
        // =====================================================================
        private void InitComboBoxes()
        {
            cboBaudRate.Items.Clear();
            cboBaudRate.Items.Add("9600");
            cboBaudRate.SelectedIndex = 0;

            RefreshComPorts();
            cboPort.DropDownOpened += (s, e) => RefreshComPorts();
        }

        private void RefreshComPorts()
        {
            if (serial.IsOpen) return;

            string[] ports = SerialService.GetPortNames();

            if (ports.SequenceEqual(cboPort.Items.Cast<string>())) return;

            string current = cboPort.SelectedItem as string;

            cboPort.Items.Clear();
            foreach (string p in ports) cboPort.Items.Add(p);

            if (current != null && ports.Contains(current)) cboPort.SelectedItem = current;
            else if (ports.Length > 0) cboPort.SelectedIndex = 0;
            else cboPort.SelectedIndex = -1;
        }

        private void SetupUIInitialState()
        {
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            btnStartStop.IsEnabled = false;
            btnTest.IsEnabled = false;
            btnExport.IsEnabled = false;
            btnReset.IsEnabled = false;
        }

        private void SetStatus(bool connected)
        {
            txtStatus.Text = connected ? "Connected" : "Disconnected";
            statusPill.Background = Hex(connected ? "#14402B" : "#4A1D2A");
            statusDot.Fill = Hex(connected ? "#3DDC97" : "#FF5C7A");
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cboPort.SelectedItem == null) return;

                if (serial.IsOpen)
                {
                    MessageBox.Show("Cổng COM đang được mở rồi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                serial.Open(new SerialSettings
                {
                    PortName = cboPort.SelectedItem.ToString(),
                    BaudRate = int.Parse(cboBaudRate.SelectedItem.ToString())
                });

                SetStatus(true);
                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                btnStartStop.IsEnabled = true;
                btnTest.IsEnabled = true;
                btnReset.IsEnabled = true;
                btnExport.IsEnabled = false;
                cboPort.IsEnabled = false;
                cboBaudRate.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối cổng COM: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                serial.Close();

                SetStatus(false);
                heartAnimator.SetRate(0);

                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
                SetMeasuring(false);
                btnStartStop.IsEnabled = false;
                btnTest.IsEnabled = false;
                btnReset.IsEnabled = false;

                cboPort.IsEnabled = true;
                cboBaudRate.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi ngắt kết nối: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        //  SEND
        // =====================================================================
        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!serial.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputData = txtSend.Text;
            if (string.IsNullOrEmpty(inputData)) return;

            try
            {
                byte[] payload = Encoding.ASCII.GetBytes(inputData);
                serial.Send(FrameBuilder.BuildCommand(payload));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi gửi dữ liệu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SendCommand(byte command)
        {
            if (!serial.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi lệnh!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                byte[] frame = FrameBuilder.BuildCommand(command);
                serial.Send(frame);
                txtSend.Text = BitConverter.ToString(frame);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi gửi lệnh: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // =====================================================================
        //  START / STOP / RESET / EXPORT
        // =====================================================================
        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (!isMeasuring) StartMeasurement();
            else StopMeasurement();
        }

        private void StartMeasurement()
        {
            if (!SendCommand(ProtocolConst.CmdStart)) return;

            ClearChartData();
            ClearStats();

            SetMeasuring(true);
            btnReset.IsEnabled = false;
            btnExport.IsEnabled = true;
        }

        private void StopMeasurement()
        {
            if (!SendCommand(ProtocolConst.CmdStop)) return;
            heartAnimator.SetRate(0);

            SetMeasuring(false);
            btnReset.IsEnabled = true;
            btnExport.IsEnabled = true;
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            SendCommand(ProtocolConst.CmdReset);
            heartAnimator.SetRate(0);

            ClearChartData();
            ClearStats();
            txtBPM.Text = "--";
            txtSpO2.Text = "--";
            btnExport.IsEnabled = false;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (ppgChart.Samples.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu sóng PPG để xuất!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|Text File (*.txt)|*.txt",
                Title = "Xuất dữ liệu đo MAX30102",
                FileName = ReportExporter.DefaultFileName()
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                ReportExporter.WriteReport(dlg.FileName, bpmStat, spo2Stat,
                                           ppgChart.Samples.FirstIndex, ppgChart.Samples.ToArray());
                MessageBox.Show("Xuất file thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        //  NHẬN KHUNG
        //  SerialService gọi OnFrameReceived từ luồng của cổng COM,
        //  nên phải chuyển sang luồng UI bằng Dispatcher trước khi đụng vào giao diện.
        // =====================================================================
        private void OnFrameReceived(DataFrame frame)
        {
            Dispatcher.BeginInvoke(new Action(() => HandleFrameOnUi(frame)));
        }

        private void HandleFrameOnUi(DataFrame frame)
        {
            txtReceive.Text = frame.Hex;

            switch (frame.Type)
            {
                case ProtocolConst.TypeVitals:
                    ShowVitals(frame);
                    break;

                case ProtocolConst.TypePpg:
                    ppgChart.AddSample(ppgFilter.Process(frame.Word));
                    break;

                case ProtocolConst.TypeTest:
                    loopbackTest.HandleFrame(frame);
                    break;
            }
        }

        private void ShowVitals(DataFrame frame)
        {
            byte bpm = frame.Data0;
            byte spo2 = frame.Data1;

            // SW-12: Kiểm tra mã lỗi cảm biến (0xFF = 255)
            if (bpm == ProtocolConst.SensorError || spo2 == ProtocolConst.SensorError)
            {
                txtBPM.Text = "ERR";
                txtSpO2.Text = "ERR";
                heartAnimator.SetRate(0);
                return;
            }

            txtBPM.Text = bpm.ToString();
            heartAnimator.SetRate(bpm);
            txtSpO2.Text = spo2 + "%";

            bpmStat.Add(bpm);
            spo2Stat.Add(spo2);
            ShowStats();
        }

        // =====================================================================
        //  TEST (logic nằm trong Testing/LoopbackTest.cs)
        // =====================================================================
        private void UpdateTestProgress(int sent)
        {
            int max = (int)progressTest.Maximum;
            if (max <= 0) return;
            sent = Math.Max(0, Math.Min(max, sent));
            progressTest.Value = sent;
            txtTestPct.Text = (sent * 100 / max) + "%";
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (!serial.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi thực hiện Test!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateTestProgress(0);

            btnTest.IsEnabled = false;
            if (!isMeasuring) btnStartStop.IsEnabled = false;
            btnDisconnect.IsEnabled = false;

            txtTestResult.Text = "Testing...";
            testResultPill.Background = Hex("#243049");

            try
            {
                LoopbackTestResult result = await loopbackTest.RunAsync();

                txtTestResult.Text = result.SuccessCount + "/" + result.Total;
                if (result.IsPerfect)
                {
                    testResultPill.Background = Hex("#14402B");
                }
                else
                {
                    testResultPill.Background = Hex("#5A4310");
                    MessageBox.Show("Bị rớt (các) gói chứa Data số: " + string.Join(", ", result.LostIndices),
                                    "Báo cáo rớt mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi Test: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTest.IsEnabled = true;
                btnStartStop.IsEnabled = true;
                btnDisconnect.IsEnabled = true;
            }
        }
    }
}
