using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using ScottPlot.Plottables;
using SPColor = ScottPlot.Color;

namespace MAX30102_WPF
{
    public partial class MainWindow : Window
    {
        // ===== SERIAL =====
        private readonly SerialPort serialPort = new SerialPort();
        private readonly List<byte> rxBuffer = new List<byte>();

        // ===== TEST =====
        private bool isTesting;
        private int testSuccessCount;
        private readonly bool[] receivedSamples = new bool[1001];

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
            InitComboBoxes();
            SetupUIInitialState();
            SetupChart();
            ClearStats();
            UpdateStartStopStyle();
            btnStartStop.IsEnabledChanged += (s, e) => UpdateStartStopStyle();

            renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)   // ~30 fps
            };
            renderTimer.Tick += OnRenderTick;
            renderTimer.Start();

            Closing += (s, e) =>
            {
                try { if (serialPort.IsOpen) serialPort.Close(); } catch (Exception) { }
            };
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
        private class RunningStat
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
                sum = 0; count = 0; Min = 0; Max = 0;
            }
        }

        private readonly RunningStat bpmStat = new RunningStat();
        private readonly RunningStat spo2Stat = new RunningStat();

        // ----- Heart icon beats at the measured heart rate -----
        private int heartAnimBpm;

        private void SetHeartRate(int bpm)
        {
            if (bpm < 30 || bpm > 220) bpm = 0;     // ngoài khoảng hợp lý: không nhấp nháy
            if (bpm == heartAnimBpm) return;
            heartAnimBpm = bpm;

            if (bpm == 0)
            {
                heartScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                heartScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                return;
            }

            // Một nhịp = "tum-tum": phình mạnh, về, phình nhẹ, về, nghỉ
            var beat = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(60000.0 / bpm),
                RepeatBehavior = RepeatBehavior.Forever
            };
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.00)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.25, KeyTime.FromPercent(0.12)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.26)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromPercent(0.38)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.52)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(1.00)));

            heartScale.BeginAnimation(ScaleTransform.ScaleXProperty, beat);
            heartScale.BeginAnimation(ScaleTransform.ScaleYProperty, beat);
        }

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
        //  PPG CHART (ScottPlot 5)
        //  X axis = absolute sample number, so a pinned line stays on the same
        //  sample while the window slides to the left.
        // =====================================================================
        private const int PPG_WINDOW = 200;

        private readonly List<double> samples = new List<double>();
        private long firstIndex;            // absolute index of samples[0]
        private double ppgDc;
        private bool chartDirty;
        private DispatcherTimer renderTimer;

        // Snapshot actually shown on the chart (stays still while frozen)
        private double[] snapY = new double[0];
        private long snapFirst;

        private Scatter ppgLine;
        private VerticalLine hoverLine;
        private Marker hoverDot;
        private double mouseX;
        private bool mouseInside;

        private class Pin
        {
            public long Index;
            public double Y;
            public string Name;
            public VerticalLine Line;
        }

        private readonly List<Pin> pins = new List<Pin>();
        private int pinCounter;
        private const int MAX_PINS = 4;
        private static readonly string[] PinColors = { "#FFC857", "#F472B6", "#A78BFA", "#60A5FA" };

        private void SetupChart()
        {
            var plt = PpgPlot.Plot;

            plt.FigureBackground.Color = SPColor.FromHex("#161E2E");
            plt.DataBackground.Color = SPColor.FromHex("#0B111B");
            plt.Axes.Color(SPColor.FromHex("#8593AD"));
            plt.Grid.MajorLineColor = SPColor.FromHex("#1F2B42");
            plt.Axes.SetLimits(0, PPG_WINDOW, -1, 1);

            // We handle the mouse ourselves (cursor line + pins), so turn off built-in pan/zoom
            PpgPlot.UserInputProcessor.IsEnabled = false;

            hoverLine = plt.Add.VerticalLine(0);
            hoverLine.Color = SPColor.FromHex("#A9B6D0");
            hoverLine.LineWidth = 1;
            hoverLine.LinePattern = ScottPlot.LinePattern.Dashed;
            hoverLine.IsVisible = false;

            PpgPlot.MouseMove += PpgPlot_MouseMove;
            PpgPlot.MouseLeave += PpgPlot_MouseLeave;
            PpgPlot.PreviewMouseLeftButtonDown += PpgPlot_Click;

            PpgPlot.Refresh();
        }

        private void AddSample(double v)
        {
            samples.Add(v);
            if (samples.Count > PPG_WINDOW)
            {
                samples.RemoveAt(0);
                firstIndex++;
            }
            chartDirty = true;
        }

        private void ClearChartData()
        {
            samples.Clear();
            firstIndex = 0;
            ppgDc = 0;
            chkFreeze.IsChecked = false;
            ClearPins();
            chartDirty = true;
        }

        private void OnRenderTick(object sender, EventArgs e)
        {
            if (!chartDirty) return;
            chartDirty = false;
            if (chkFreeze.IsChecked == true) return;   // frozen: keep the picture still
            RebuildChart();
        }

        private void RebuildChart()
        {
            var plt = PpgPlot.Plot;

            if (ppgLine != null) { plt.Remove(ppgLine); ppgLine = null; }

            snapY = samples.ToArray();
            snapFirst = firstIndex;

            // Drop pins that have slid out of the window
            for (int k = pins.Count - 1; k >= 0; k--)
            {
                if (pins[k].Index < snapFirst)
                {
                    plt.Remove(pins[k].Line);
                    pins.RemoveAt(k);
                }
            }

            if (snapY.Length > 1)
            {
                double[] xs = new double[snapY.Length];
                for (int i = 0; i < xs.Length; i++) xs[i] = snapFirst + i;

                ppgLine = plt.Add.Scatter(xs, snapY);
                ppgLine.Color = SPColor.FromHex("#3DDC97");
                ppgLine.LineWidth = 2;
                ppgLine.MarkerSize = 0;

                double min = snapY.Min();
                double max = snapY.Max();
                double pad = (max - min) * 0.1;
                if (pad < 1) pad = 1;
                plt.Axes.SetLimitsY(min - pad, max + pad);
            }
            plt.Axes.SetLimitsX(snapFirst, snapFirst + PPG_WINDOW - 1);

            UpdatePinText();
            UpdateHover();
            PpgPlot.Refresh();
        }

        private void chkFreeze_Changed(object sender, RoutedEventArgs e)
        {
            // Unfreezing: redraw straight away with the newest data
            if (chkFreeze.IsChecked != true) { chartDirty = true; }
        }

        // ----- Hover line -----
        private double MouseToDataX(System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(PpgPlot);
            var px = new ScottPlot.Pixel((float)(p.X * PpgPlot.DisplayScale), (float)(p.Y * PpgPlot.DisplayScale));
            return PpgPlot.Plot.GetCoordinates(px).X;
        }

        private void PpgPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouseX = MouseToDataX(e);
            mouseInside = true;
            UpdateHover();
            PpgPlot.Refresh();
        }

        private void PpgPlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouseInside = false;
            UpdateHover();
            PpgPlot.Refresh();
        }

        private void UpdateHover()
        {
            var plt = PpgPlot.Plot;
            if (hoverDot != null) { plt.Remove(hoverDot); hoverDot = null; }

            int i = (int)Math.Round(mouseX - snapFirst);
            if (!mouseInside || snapY.Length == 0 || i < 0 || i >= snapY.Length)
            {
                hoverLine.IsVisible = false;
                txtHover.Text = "Move the mouse over the chart to read values.";
                return;
            }

            double x = snapFirst + i;
            double y = snapY[i];
            hoverLine.X = x;
            hoverLine.IsVisible = true;
            hoverDot = plt.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, 9, SPColor.FromHex("#FFFFFF"));
            txtHover.Text = string.Format(CultureInfo.InvariantCulture, "Sample #{0}    PPG = {1:0.0}", (long)x, y);
        }

        // ----- Pins (click to clone a line at that position) -----
        private void PpgPlot_Click(object sender, MouseButtonEventArgs e)
        {
            int i = (int)Math.Round(MouseToDataX(e) - snapFirst);
            if (snapY.Length == 0 || i < 0 || i >= snapY.Length) return;

            long abs = snapFirst + i;
            if (pins.Any(p => p.Index == abs)) return;

            var line = PpgPlot.Plot.Add.VerticalLine(abs);
            line.LineWidth = 2;
            line.Color = SPColor.FromHex(PinColors[pinCounter % PinColors.Length]);

            pins.Add(new Pin
            {
                Index = abs,
                Y = snapY[i],
                Name = ((char)('A' + pinCounter % 26)).ToString(),
                Line = line
            });
            pinCounter++;

            if (pins.Count > MAX_PINS)
            {
                PpgPlot.Plot.Remove(pins[0].Line);
                pins.RemoveAt(0);
            }

            UpdatePinText();
            PpgPlot.Refresh();
        }

        private void UpdatePinText()
        {
            if (pins.Count == 0)
            {
                txtPins.Text = "Click on the chart to pin a line and compare.";
                return;
            }

            string text = string.Join("    ",
                pins.Select(p => string.Format(CultureInfo.InvariantCulture, "{0}: #{1} = {2:0.0}", p.Name, p.Index, p.Y)));

            if (pins.Count >= 2)
            {
                Pin a = pins[pins.Count - 2];
                Pin b = pins[pins.Count - 1];
                text += string.Format(CultureInfo.InvariantCulture,
                    "\nΔ {0}→{1}: {2} samples, ΔPPG = {3:+0.0;-0.0;0.0}",
                    a.Name, b.Name, b.Index - a.Index, b.Y - a.Y);
            }
            txtPins.Text = text;
        }

        private void ClearPins()
        {
            foreach (Pin p in pins) PpgPlot.Plot.Remove(p.Line);
            pins.Clear();
            pinCounter = 0;
            UpdatePinText();
        }

        private void btnClearPins_Click(object sender, RoutedEventArgs e)
        {
            ClearPins();
            PpgPlot.Refresh();
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
            if (serialPort.IsOpen) return;

            string[] ports = SerialPort.GetPortNames()
                                       .OrderBy(p => p.Length)
                                       .ThenBy(p => p)
                                       .ToArray();

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

                if (serialPort.IsOpen)
                {
                    MessageBox.Show("Cổng COM đang được mở rồi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                serialPort.PortName = cboPort.SelectedItem.ToString();
                serialPort.BaudRate = int.Parse(cboBaudRate.SelectedItem.ToString());

                // Bỏ qua tín hiệu bắt tay phần cứng (tránh lỗi driver CH340)
                serialPort.DtrEnable = false;
                serialPort.RtsEnable = false;

                serialPort.DataReceived -= SerialPort_DataReceived;
                serialPort.DataReceived += SerialPort_DataReceived;

                serialPort.Open();

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
                if (serialPort.IsOpen)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                }

                SetStatus(false);
                SetHeartRate(0);

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
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputData = txtSend.Text;
            if (string.IsNullOrEmpty(inputData)) return;

            try
            {
                byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(inputData);

                // Header(1) + TypeData(1) + Data(N) + Footer(1) + CRC(1)
                int frameLength = 4 + dataBytes.Length;
                byte[] frame = new byte[frameLength];

                frame[0] = 0x40;       // '@'
                frame[1] = 0x02;       // lệnh từ PC xuống

                byte crc = frame[1];
                for (int i = 0; i < dataBytes.Length; i++)
                {
                    frame[2 + i] = dataBytes[i];
                    crc ^= dataBytes[i];
                }

                frame[2 + dataBytes.Length] = 0x26;   // '&'
                crc ^= 0x26;
                frame[frameLength - 1] = crc;

                serialPort.Write(frame, 0, frame.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi gửi dữ liệu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SendCommand(char commandChar)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi lệnh!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                byte[] frame = new byte[5];
                frame[0] = 0x40;               // '@'
                frame[1] = 0x02;               // lệnh điều khiển
                frame[2] = (byte)commandChar;  // '1', '0' hoặc 'R'
                frame[3] = 0x26;               // '&'
                frame[4] = (byte)(frame[1] ^ frame[2] ^ frame[3]);

                serialPort.Write(frame, 0, frame.Length);
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
            if (!SendCommand('1')) return;

            ClearChartData();
            ClearStats();

            SetMeasuring(true);
            btnReset.IsEnabled = false;
            btnExport.IsEnabled = true;
        }

        private void StopMeasurement()
        {
            if (!SendCommand('0')) return;
            SetHeartRate(0);

            SetMeasuring(false);
            btnReset.IsEnabled = true;
            btnExport.IsEnabled = true;
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            SendCommand('R');
            SetHeartRate(0);

            ClearChartData();
            ClearStats();
            txtBPM.Text = "--";
            txtSpO2.Text = "--";
            btnExport.IsEnabled = false;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (samples.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu sóng PPG để xuất!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|Text File (*.txt)|*.txt",
                Title = "Xuất dữ liệu đo MAX30102",
                FileName = "PPG_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var sw = new StreamWriter(dlg.FileName))
                {
                    sw.WriteLine("=== MAX30102 HEALTH MONITOR REPORT ===");
                    sw.WriteLine("Export Time," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sw.WriteLine("BPM Average," + (bpmStat.HasData ? bpmStat.Avg.ToString("0.0", CultureInfo.InvariantCulture) : "--"));
                    sw.WriteLine("BPM Min / Max," + (bpmStat.HasData ? bpmStat.Min + " / " + bpmStat.Max : "-- / --"));
                    sw.WriteLine("SpO2 Average," + (spo2Stat.HasData ? spo2Stat.Avg.ToString("0.0", CultureInfo.InvariantCulture) : "--"));
                    sw.WriteLine("SpO2 Min / Max," + (spo2Stat.HasData ? spo2Stat.Min + " / " + spo2Stat.Max : "-- / --"));
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
                MessageBox.Show("Xuất file thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        //  RECEIVE + FRAME PARSING
        // =====================================================================
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = serialPort.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] temp = new byte[bytesToRead];
                serialPort.Read(temp, 0, bytesToRead);

                lock (rxBuffer)
                {
                    rxBuffer.AddRange(temp);
                }
                ProcessRxBuffer();
            }
            catch (Exception)
            {
                // Bỏ qua ngoại lệ khi ngắt kết nối đột ngột
            }
        }

        private void ProcessRxBuffer()
        {
            lock (rxBuffer)
            {
                while (rxBuffer.Count >= 4)
                {
                    if (rxBuffer[0] != 0x40)
                    {
                        rxBuffer.RemoveAt(0);
                        continue;
                    }

                    byte typeData = rxBuffer[1];
                    int frameLength;
                    bool hasCRC;

                    if (typeData == 0x01 || typeData == 0x03)
                    {
                        frameLength = 6;
                        hasCRC = true;
                    }
                    else if (typeData == 0x04)
                    {
                        frameLength = 5;
                        hasCRC = false;
                    }
                    else
                    {
                        rxBuffer.RemoveAt(0);
                        continue;
                    }

                    if (rxBuffer.Count < frameLength) break;

                    byte[] frame = rxBuffer.GetRange(0, frameLength).ToArray();
                    int footerIndex = hasCRC ? (frameLength - 2) : (frameLength - 1);

                    if (frame[footerIndex] == 0x26)
                    {
                        if (hasCRC)
                        {
                            byte calculatedCRC = (byte)(frame[1] ^ frame[2] ^ frame[3] ^ frame[4]);
                            if (calculatedCRC == frame[5]) DisplayReceivedFrame(frame);
                        }
                        else
                        {
                            DisplayReceivedFrame(frame);
                        }
                    }

                    rxBuffer.RemoveRange(0, frameLength);
                }
            }
        }

        private void DisplayReceivedFrame(byte[] frame)
        {
            string rawHex = BitConverter.ToString(frame);
            byte typeData = frame[1];

            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtReceive.Text = rawHex;

                if (typeData == 0x01)
                {
                    byte bpm = frame[2];
                    byte spo2 = frame[3];
                    txtBPM.Text = bpm.ToString();
                    SetHeartRate(bpm);
                    txtSpO2.Text = spo2 + "%";

                    bpmStat.Add(bpm);
                    spo2Stat.Add(spo2);
                    ShowStats();
                }
                else if (typeData == 0x03)
                {
                    ushort ppgRaw = (ushort)((frame[2] << 8) | frame[3]);

                    if (ppgDc == 0) ppgDc = ppgRaw;
                    ppgDc += (ppgRaw - ppgDc) * 0.05;     // trung bình trượt chậm = thành phần DC
                    AddSample(ppgDc - ppgRaw);            // đảo dấu để đỉnh sóng ứng với nhịp tim
                }
                else if (typeData == 0x04)
                {
                    if (isTesting)
                    {
                        ushort d = (ushort)((frame[2] << 8) | frame[3]);
                        if (d >= 1 && d <= 1000 && !receivedSamples[d])
                        {
                            receivedSamples[d] = true;
                            testSuccessCount++;
                        }
                    }
                }
            }));
        }

        // =====================================================================
        //  TEST (1000 mẫu)
        // =====================================================================
        private void SendTestFrame(ushort data)
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            byte[] frame = new byte[5];
            frame[0] = 0x40;
            frame[1] = 0x04;
            frame[2] = (byte)(data >> 8);
            frame[3] = (byte)(data & 0xFF);
            frame[4] = 0x26;

            serialPort.Write(frame, 0, frame.Length);
            txtSend.Text = BitConverter.ToString(frame);
        }

        private void SendTestControlFrame(byte cmd2)
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            byte[] frame = new byte[5];
            frame[0] = 0x40;
            frame[1] = 0x04;
            frame[2] = 0x84;   // 'T' (Test command)
            frame[3] = cmd2;   // 0x83 = bắt đầu, 0x69 = kết thúc
            frame[4] = 0x26;

            serialPort.Write(frame, 0, frame.Length);
            txtSend.Text = BitConverter.ToString(frame);
        }

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
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi thực hiện Test!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isTesting = true;
            testSuccessCount = 0;
            Array.Clear(receivedSamples, 0, receivedSamples.Length);
            UpdateTestProgress(0);

            btnTest.IsEnabled = false;
            if (!isMeasuring) btnStartStop.IsEnabled = false;
            btnDisconnect.IsEnabled = false;

            txtTestResult.Text = "Testing...";
            testResultPill.Background = Hex("#243049");

            try
            {
                SendTestControlFrame(0x83);
                await Task.Delay(50);

                for (int i = 1; i <= 1000; i++)
                {
                    if (!serialPort.IsOpen) break;
                    SendTestFrame((ushort)i);
                    UpdateTestProgress(i);
                    await Task.Delay(30);
                }

                int timeoutMs = 40000;
                int delayInterval = 100;
                int elapsedMs = 0;
                while (elapsedMs < timeoutMs && testSuccessCount < 1000)
                {
                    await Task.Delay(delayInterval);
                    elapsedMs += delayInterval;
                }

                SendTestControlFrame(0x69);

                txtTestResult.Text = testSuccessCount + "/1000";
                if (testSuccessCount == 1000)
                {
                    testResultPill.Background = Hex("#14402B");
                }
                else
                {
                    testResultPill.Background = Hex("#5A4310");
                    var lost = new List<string>();
                    for (int i = 1; i <= 1000; i++)
                        if (!receivedSamples[i]) lost.Add(i.ToString());

                    MessageBox.Show("Bị rớt (các) gói chứa Data số: " + string.Join(", ", lost),
                                    "Báo cáo rớt mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi Test: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isTesting = false;
                btnTest.IsEnabled = true;
                btnStartStop.IsEnabled = true;
                btnDisconnect.IsEnabled = true;
            }
        }
    }
}
