using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MAX30102_GUI
{
    public partial class form1 : Form
    {
        // Khai báo cổng Serial
        private SerialPort serialPort = new SerialPort();
        private List<byte> rxBuffer = new List<byte>();
        private bool isTesting = false;                   // Cờ báo đang trong quá trình Test
        private int testSuccessCount = 0;                 // Bộ đếm số mẫu nhận đúng
        private bool[] receivedSamples = new bool[1001];  // Đánh dấu các mẫu từ 1-1000 đã nhận, tránh đếm trùng
        private bool receivedTeResponse = false;          // Cờ báo đã nhận lại phản hồi TE từ Firmware
        public form1()
        {
            InitializeComponent();
            SetupUIInitialState();
            InitComboBoxes();
            SetupChart();
            SetupActionButtons();
            SetupProgressText();
            SetupStatsToggle();
        }
        private void form1_Load(object sender, EventArgs e)
        {
            // Cấu hình COM/Baudrate và đồ thị đã chuyển sang constructor
            // (InitComboBoxes, SetupChart) nên không phụ thuộc vào sự kiện Load nữa.
        }
        // ===== NÚT START / STOP (GỘP 1 NÚT btnStartStop) =====
        private bool isMeasuring = false;                        // false: đang chờ Start | true: đang đo
        private readonly Color startColor = Color.FromArgb(46, 160, 67);    // xanh lá
        private readonly Color stopColor = Color.FromArgb(214, 48, 49);     // đỏ

        private void SetupActionButtons()
        {
            btnStartStop.Click -= btnStartStop_Click;   // tránh bị gắn trùng nếu Designer đã gắn sẵn
            btnStartStop.Click += btnStartStop_Click;
            btnStartStop.EnabledChanged += (s, e) => UpdateStartStopStyle();
            UpdateStartStopStyle();
        }

        private void SetMeasuring(bool measuring)
        {
            isMeasuring = measuring;
            UpdateStartStopStyle();
        }

        // Chữ + màu theo trạng thái: Start (xanh) / Stop (đỏ); bị Disable thì trả về giao diện xám mặc định
        private void UpdateStartStopStyle()
        {
            btnStartStop.Text = isMeasuring ? "Stop" : "Start";
            if (btnStartStop.Enabled)
            {
                btnStartStop.BackColor = isMeasuring ? stopColor : startColor;
                btnStartStop.ForeColor = isMeasuring ? Color.Black : Color.White;
            }
            else
            {
                btnStartStop.BackColor = SystemColors.Control;
                btnStartStop.UseVisualStyleBackColor = true;
                btnStartStop.ForeColor = SystemColors.ControlText;
            }
        }

        // ===== GIAO DIỆN: CHỮ % TRÊN PROGRESS BAR =====
        private ProgressTextPainter testProgressPainter;

        private void SetupProgressText()
        {
            // Label không còn nằm đè lên thanh (nên hết khung nền); chỉ giữ lại để chứa chữ, font, màu chữ
            lblTestProgress.Visible = false;
            testProgressPainter = new ProgressTextPainter(progressBarTest, lblTestProgress);
        }

        // Vẽ trực tiếp chữ lên ProgressBar sau mỗi lần thanh tự vẽ lại
        private class ProgressTextPainter : NativeWindow
        {
            private const int WM_PAINT = 0x000F;
            private readonly ProgressBar bar;
            private readonly Label source;

            public ProgressTextPainter(ProgressBar bar, Label source)
            {
                this.bar = bar;
                this.source = source;
                bar.HandleCreated += (s, e) => AssignHandle(bar.Handle);
                bar.HandleDestroyed += (s, e) => ReleaseHandle();
                if (bar.IsHandleCreated) AssignHandle(bar.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WM_PAINT)
                {
                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        TextRenderer.DrawText(g, source.Text, source.Font, bar.ClientRectangle, source.ForeColor,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    }
                }
            }
        }

        // ===== THỐNG KÊ BPM / SpO2 (Avg - Min - Max) =====
        // Giá trị 0 (chưa đo được) bị bỏ qua, không đưa vào Avg / Min / Max
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
                sum = 0;
                count = 0;
                Min = 0;
                Max = 0;
            }
        }

        private readonly RunningStat bpmStat = new RunningStat();
        private readonly RunningStat spo2Stat = new RunningStat();

        private void ShowStats()
        {
            ShowStat(bpmStat, txtBPM_Avg, txtBPM_Min, txtBPM_Max, "");
            ShowStat(spo2Stat, txtSpO2_Avg, txtSpO2_Min, txtSpO2_Max, "%");
        }

        private void ShowStat(RunningStat s, TextBox avg, TextBox min, TextBox max, string unit)
        {
            if (!s.HasData)
            {
                avg.Text = min.Text = max.Text = "--";
                return;
            }
            avg.Text = s.Avg.ToString("0.0") + unit;
            min.Text = s.Min + unit;
            max.Text = s.Max + unit;
        }

        private void ClearStats()
        {
            bpmStat.Clear();
            spo2Stat.Clear();
            ShowStats();   // hiện "--"
        }

        // ===== NÚT ▼ / ▲ ẨN - HIỆN 3 Ô Avg / Min / Max =====
        private void SetupStatsToggle()
        {
            SetupToggleButton("btnBPMToggle", txtBPM,
                lblBPM_Avg, txtBPM_Avg, lblBPM_Min, txtBPM_Min, lblBPM_Max, txtBPM_Max);
            SetupToggleButton("btnSpO2Toggle", txtSpO2,
                lblSpO2_Avg, txtSpO2_Avg, lblSpO2_Min, txtSpO2_Min, lblSpO2_Max, txtSpO2_Max);
        }

        // Dùng nút có sẵn trong Designer (cùng tên) nếu có, giữ nguyên Size/Location bạn đặt.
        // Chưa có thì tự tạo ngay bên phải ô chính. Mặc định ẩn hàng Avg/Min/Max (khoảng trống vẫn giữ nguyên).
        private void SetupToggleButton(string name, TextBox anchor, params Control[] items)
        {
            Button btn = this.Controls.Find(name, true).OfType<Button>().FirstOrDefault();
            if (btn == null)
            {
                btn = new Button();
                btn.Name = name;
                btn.Size = new Size(anchor.Height + 6, anchor.Height);   // kích thước nút
                btn.Location = new Point(anchor.Right + 6, anchor.Top);  // vị trí: bên phải ô chính
                anchor.Parent.Controls.Add(btn);
            }

            btn.Text = "\u25BC";                                   // ▼
            btn.Font = new Font("Segoe UI Symbol", 8f);              // đổi 8f để phóng to / thu nhỏ mũi tên
            btn.TabStop = false;
            btn.Cursor = Cursors.Hand;

            // Kiểu phẳng, không viền: chỉ thấy mỗi mũi tên, di chuột vào mới hiện nền xám nhạt
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 225, 225);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(205, 205, 205);
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Color.FromArgb(64, 64, 64);

            foreach (Control c in items) c.Visible = false;

            bool expanded = false;
            btn.Click += (s, e) =>
            {
                expanded = !expanded;
                foreach (Control c in items) c.Visible = expanded;
                btn.Text = expanded ? "\u25B2" : "\u25BC";   // ▲ khi đang hiện, ▼ khi đang ẩn
            };
        }

        // ===== ĐỒ THỊ PPG =====
        private const int PPG_WINDOW = 200;   // Số mẫu hiển thị cùng lúc (cửa sổ trượt FIFO)
        private double ppgDc = 0;
        private void SetupChart()
        {
            // 1. Chỉ 1 đường PPG, dạng line, màu xanh lá
            chartPPG.Series.Clear();
            var series = chartPPG.Series.Add("PPG");
            series.ChartType = SeriesChartType.FastLine;   // Tối ưu vẽ realtime
            series.Color = Color.LimeGreen;
            series.BorderWidth = 2;

            // 2. Bỏ chú thích (Legend) cho rộng chỗ
            chartPPG.Legends.Clear();

            var area = chartPPG.ChartAreas[0];

            // 3. Trục thời gian cố định 1..PPG_WINDOW: đồ thị không bị co giãn,
            //    mẫu mới vào bên phải, mẫu cũ trượt ra bên trái như điện tâm đồ
            area.AxisX.Minimum = 1;
            area.AxisX.Maximum = PPG_WINDOW;
            area.AxisX.LabelStyle.Enabled = false;     // trục thời gian không hiện số
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisX.MajorGrid.Enabled = false;

            // 4. Trục biên độ: chỉ trục này hiện số, tự co giãn bám sát sóng
            area.AxisY.LabelStyle.Enabled = true;
            area.AxisY.MajorTickMark.Enabled = true;
            area.AxisY.IsStartedFromZero = false;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
        }

        // ===== COM PORT & BAUDRATE =====
        private const int WM_DEVICECHANGE = 0x0219;   // Windows báo có thiết bị cắm vào / rút ra

        private void InitComboBoxes()
        {
            // Baudrate: yêu cầu đề là 9600 -> giữ combo box nhưng chọn sẵn
            cboBaudRate.Items.Clear();
            cboBaudRate.Items.Add("9600");
            cboBaudRate.SelectedIndex = 0;

            // COM Port: tự quét các cổng đang có (giống Device Manager)
            RefreshComPorts();

            // Mỗi lần bấm mở danh sách cũng quét lại cho chắc
            cboPort.DropDown += (s, e) => RefreshComPorts();
        }

        private void RefreshComPorts()
        {
            // Đang kết nối thì không đụng vào danh sách
            if (serialPort.IsOpen) return;

            // Sắp xếp COM2 trước COM10
            string[] ports = SerialPort.GetPortNames()
                                       .OrderBy(p => p.Length)
                                       .ThenBy(p => p)
                                       .ToArray();

            // Danh sách không đổi thì thôi, tránh nháy khi đang mở dropdown
            if (ports.SequenceEqual(cboPort.Items.Cast<string>())) return;

            string current = cboPort.SelectedItem as string;

            cboPort.BeginUpdate();
            cboPort.Items.Clear();
            cboPort.Items.AddRange(ports);
            cboPort.EndUpdate();

            if (current != null && ports.Contains(current))
                cboPort.SelectedItem = current;      // giữ lại cổng đang chọn nếu nó vẫn còn
            else if (ports.Length > 0)
                cboPort.SelectedIndex = 0;           // không thì chọn cổng đầu tiên
            else
                cboPort.SelectedIndex = -1;          // không có cổng nào
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_DEVICECHANGE)
            {
                RefreshComPorts();   // cắm / rút USB-UART là danh sách tự cập nhật
            }
        }

        private void SetupUIInitialState()
        {
            // Thiết lập trạng thái các nút ban đầu khi chưa Connect
            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            btnStartStop.Enabled = false;
            btnTest.Enabled = false;
            btnExport.Enabled = false;
            btnReset.Enabled = false;
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (cboPort.SelectedItem == null) return;

                // KIỂM TRA: Nếu cổng đã mở từ trước thì không gán lại PortName nữa
                if (serialPort.IsOpen)
                {
                    MessageBox.Show("Cổng COM đang được mở rồi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Thiết lập cấu hình cổng COM
                serialPort.PortName = cboPort.SelectedItem.ToString();
                serialPort.BaudRate = int.Parse(cboBaudRate.SelectedItem.ToString());

                // Bỏ qua tín hiệu bắt tay phần cứng (Tránh lỗi driver CH340)
                serialPort.DtrEnable = false;
                serialPort.RtsEnable = false;

                // Bắt sự kiện nhận dữ liệu
                serialPort.DataReceived -= SerialPort_DataReceived; // Xóa gán cũ nếu có
                serialPort.DataReceived += SerialPort_DataReceived; // Gán lại sự kiện mới

                // Mở cổng kết nối
                serialPort.Open();
                MessageBox.Show("COM Port " + cboPort.Text + " is connected.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Cập nhật trạng thái giao diện UI
                txtStatus.Text = "Connected";
                txtStatus.BackColor = Color.LightGreen;
                btnDisconnect.Focus();
                // Quản lý trạng thái nút bấm
                btnConnect.Enabled = false;       // Disable nút Connect
                btnDisconnect.Enabled = true;     // Enable nút Disconnect
                btnStartStop.Enabled = true;      // Enable nút Start/Stop
                btnTest.Enabled = true;           // Enable nút Test
                btnReset.Enabled = true;
                btnExport.Enabled = false;         // Enable nút Export
                cboPort.Enabled = false;       // Khóa chọn COM khi đang kết nối
                cboBaudRate.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối cổng COM: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Đóng cổng Serial nếu đang mở
                if (serialPort.IsOpen)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived; // Hủy bắt sự kiện
                    serialPort.Close();                                 // Đóng cổng
                }

                // 2. Cập nhật trạng thái hiển thị ô txtStatus
                txtStatus.Text = "Disconnected";
                txtStatus.BackColor = Color.Red;

                // 3. Khôi phục trạng thái thao tác nút bấm
                btnConnect.Enabled = true;       // Mở lại nút Connect
                btnDisconnect.Enabled = false;   // Vô hiệu hóa nút Disconnect
                SetMeasuring(false);             // về lại chế độ Start
                btnStartStop.Enabled = false;    // Tắt nút Start/Stop
                btnTest.Enabled = false;         // Tắt nút Test
                btnReset.Enabled = false;

                // 4. Mở lại các ô chọn cổng COM/BaudRate
                cboPort.Enabled = true;
                cboBaudRate.Enabled = true;

                btnConnect.Focus(); // Đẩy Focus về lại nút Connect
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi ngắt kết nối: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            // 1. Kiểm tra trạng thái cổng COM
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Lấy dữ liệu dạng text từ ô txtSend
            string inputData = txtSend.Text;
            if (string.IsNullOrEmpty(inputData)) return;

            try
            {
                // Chuyển chuỗi chữ bạn nhập thành mảng Byte (mã ASCII)
                byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(inputData);

                // Tổng kích thước frame = Header(1) + TypeData(1) + Data(N) + Footer(1) + CRC(1)
                int frameLength = 4 + dataBytes.Length;
                byte[] frame = new byte[frameLength];

                // --- BẮT ĐẦU ĐÓNG GÓI FRAME ---

                frame[0] = 0x40;       // Header: '@'
                frame[1] = 0x02;       // TypeData: 0x02 (Đại diện cho lệnh từ PC gửi xuống)

                byte crc = frame[1];   // Bắt đầu tính CRC (XOR) bằng TypeData

                // Đổ Data vào giữa và thực hiện phép toán XOR liên tiếp cho CRC
                for (int i = 0; i < dataBytes.Length; i++)
                {
                    frame[2 + i] = dataBytes[i];
                    crc ^= dataBytes[i];
                }

                int footerIndex = 2 + dataBytes.Length;
                frame[footerIndex] = 0x26; // Footer: '&'
                crc ^= 0x26;               // Nhớ XOR luôn cả Footer vào CRC

                // Chốt byte CRC ở vị trí cuối cùng của mảng
                frame[frameLength - 1] = crc;

                // --- GỬI XUỐNG KIT ---
                serialPort.Write(frame, 0, frame.Length);

                // (Tùy chọn) Ghi đè ô txtSend thành chuỗi Hex vừa gửi để thấy rõ Format đã đúng chưa
                //txtSend.Text = BitConverter.ToString(frame);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi gửi dữ liệu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = serialPort.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] tempBuffer = new byte[bytesToRead];
                serialPort.Read(tempBuffer, 0, bytesToRead);

                // Nạp dữ liệu vào bộ đệm tĩnh
                lock (rxBuffer)
                {
                    rxBuffer.AddRange(tempBuffer);
                }

                // Gọi hàm quét và tách frame
                ProcessRxBuffer();
            }
            catch (System.Exception)
            {
                // Bỏ qua ngoại lệ ngắt kết nối đột ngột
            }
        }
        private void SendCommand(char commandChar)
        {
            // 1. Kiểm tra trạng thái cổng COM
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi gửi lệnh!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Kịch bản B: Khung truyền cố định 5 Bytes
                byte[] frame = new byte[5];

                frame[0] = 0x40;               // Header: '@'
                frame[1] = 0x02;               // TypeData: 0x02 (Lệnh điều khiển)
                frame[2] = (byte)commandChar;  // Data: Ký tự lệnh ('1', '0', hoặc 'R')
                frame[3] = 0x26;               // Footer: '&'

                // Tính CRC: XOR của TypeData ^ Data ^ Footer
                frame[4] = (byte)(frame[1] ^ frame[2] ^ frame[3]);

                // Gửi xuống kit
                serialPort.Write(frame, 0, frame.Length);

                // Hiển thị chuỗi Hex vừa gửi lên ô txtSend để bạn dễ giám sát
                if (txtSend != null)
                {
                    txtSend.Text = BitConverter.ToString(frame);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi gửi lệnh: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (!isMeasuring) StartMeasurement();
            else StopMeasurement();
        }
        private void StartMeasurement()
        {
            // Kịch bản B: lệnh Start, ký tự '1' (0x31)
            SendCommand('1');

            chartPPG.Series[0].Points.Clear();   // đồ thị vẽ lại từ đầu
            ppgDc = 0;
            ClearStats();

            SetMeasuring(true);                  // nút chuyển sang Stop (đỏ)
            btnReset.Enabled = false;
            btnExport.Enabled = true;
        }
        private void StopMeasurement()
        {
            // Kịch bản B: lệnh Stop, ký tự '0' (0x30)
            SendCommand('0');

            SetMeasuring(false);                 // nút chuyển về Start (xanh)
            btnReset.Enabled = true;
            btnExport.Enabled = true;
        }
        private void SendTestFrame(ushort data)
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            byte[] frame = new byte[5];
            frame[0] = 0x40;
            frame[1] = 0x04;
            frame[2] = (byte)(data >> 8);   // Tách Byte cao
            frame[3] = (byte)(data & 0xFF); // Tách Byte thấp
            frame[4] = 0x26;

            serialPort.Write(frame, 0, frame.Length);
            if (txtSend != null) txtSend.Text = BitConverter.ToString(frame);
        }
        private async void btnTest_Click(object sender, EventArgs e)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng CONNECT cổng COM trước khi thực hiện Test!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isTesting = true;
            testSuccessCount = 0;
            Array.Clear(receivedSamples, 0, receivedSamples.Length);
            receivedTeResponse = false;

            // Reset thanh tiến độ về 0% (tiến độ tính theo số mẫu đã GỬI)
            progressBarTest.Minimum = 0;
            progressBarTest.Maximum = 1000;   // = tổng số mẫu sẽ gửi
            UpdateTestProgress(0);

            btnTest.Enabled = false;
            if (!isMeasuring) btnStartStop.Enabled = false;   // đang đo thì vẫn giữ nút Stop để dừng được
            btnDisconnect.Enabled = false;

            if (txtTestResult != null)
            {
                txtTestResult.Text = "Testing...";
                txtTestResult.BackColor = Color.White;
            }
            try
            {
                SendTestControlFrame(0x83);
                await Task.Delay(50);

                // Gửi 1000 mẫu dữ liệu
                for (int i = 1; i <= 1000; i++)
                {
                    if (!serialPort.IsOpen) break;
                    SendTestFrame((ushort)i);
                    UpdateTestProgress(i);
                    await Task.Delay(30);
                }

                // Nâng thời gian chờ tối đa lên 35-40 giây (vì 1000 mẫu x 30ms đã là 30 giây)
                int timeoutMs = 40000;
                int delayInterval = 100;
                int elapsedMs = 0;

                while (elapsedMs < timeoutMs && testSuccessCount < 1000)
                {
                    await Task.Delay(delayInterval);
                    elapsedMs += delayInterval;
                }

                SendTestControlFrame(0x69);

                if (txtTestResult != null)
                {
                    txtTestResult.Text = $"{testSuccessCount}/1000";
                    if (testSuccessCount == 1000)
                    {
                        txtTestResult.BackColor = Color.Green;
                    }
                    else
                    {
                        txtTestResult.BackColor = Color.Yellow;
                        List<string> lostBytes = new List<string>();
                        for (int i = 1; i <= 1000; i++)
                        {
                            if (!receivedSamples[i]) lostBytes.Add(i.ToString());
                        }
                        MessageBox.Show("Bị rớt (các) gói chứa Data số: " + string.Join(", ", lostBytes),
                                        "Báo cáo rớt mạng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi Test: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isTesting = false;
                btnTest.Enabled = true;
                btnStartStop.Enabled = true;
                btnDisconnect.Enabled = true;
            }
        }
        private void UpdateTestProgress(int sent)
        {
            // sent = số mẫu đã gửi (0..1000)
            lblTestProgress.Text = (sent * 100 / progressBarTest.Maximum) + "%";
            progressBarTest.Value = sent;
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
                    int frameLength = 0;
                    bool hasCRC = true;

                    // Kịch bản A/C có CRC (6 byte), Kịch bản Test 4 byte
                    if (typeData == 0x01 || typeData == 0x03)
                    {
                        frameLength = 6;
                        hasCRC = true;
                    }
                    else if (typeData == 0x04)
                    {
                        frameLength = 5; // Cập nhật chiều dài mới
                        hasCRC = false;
                    }
                    else
                    {
                        rxBuffer.RemoveAt(0);
                        continue;
                    }

                    if (rxBuffer.Count < frameLength) break;

                    byte[] frame = rxBuffer.GetRange(0, frameLength).ToArray();

                    // SỬA LỖI Ở ĐÂY: Vị trí của Footer phụ thuộc vào việc có CRC hay không
                    int footerIndex = hasCRC ? (frameLength - 2) : (frameLength - 1);

                    // Kiểm tra Footer có đúng là '&' (0x26) hay không
                    if (frame[footerIndex] == 0x26)
                    {
                        if (hasCRC)
                        {
                            // Tính CRC từ TypeData đến Footer
                            byte calculatedCRC = (byte)(frame[1] ^ frame[2] ^ frame[3] ^ frame[4]);
                            if (calculatedCRC == frame[5])
                            {
                                DisplayReceivedFrame(frame);
                            }
                        }
                        else
                        {
                            DisplayReceivedFrame(frame); // Frame Test cho qua luôn
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

            this.BeginInvoke(new Action(() =>
            {
                if (txtReceive != null) txtReceive.Text = rawHex;

                if (typeData == 0x01)
                {
                    byte bpm = frame[2];
                    byte spo2 = frame[3];
                    if (txtBPM != null) txtBPM.Text = bpm.ToString();
                    if (txtSpO2 != null) txtSpO2.Text = spo2.ToString() + "%";

                    bpmStat.Add(bpm);
                    spo2Stat.Add(spo2);
                    ShowStats();
                }
                else if (typeData == 0x03)
                {
                    ushort ppgRaw = (ushort)((frame[2] << 8) | frame[3]);
                    if (chartPPG.Series.Count > 0)
                    {
                        if (ppgDc == 0) ppgDc = ppgRaw;               // mẫu đầu tiên
                        ppgDc += (ppgRaw - ppgDc) * 0.05;             // trung bình trượt chậm (~0,8 s) = thành phần DC
                        double ac = ppgDc - ppgRaw;                   // đảo dấu để đỉnh sóng ứng với nhịp tim
                        chartPPG.Series[0].Points.AddY(ac);
                        if (chartPPG.Series[0].Points.Count > PPG_WINDOW) chartPPG.Series[0].Points.RemoveAt(0);
                    }
                }
                else if (typeData == 0x04)
                {
                    if (isTesting)
                    {
                        // Ghép Byte cao và Byte thấp lại thành con số ban đầu
                        ushort d = (ushort)((frame[2] << 8) | frame[3]);

                        if (d >= 1 && d <= 1000)
                        {
                            if (!receivedSamples[d])
                            {
                                receivedSamples[d] = true;
                                testSuccessCount++;
                            }
                        }
                    }
                }
            }));
        }
        private void SendTestControlFrame(byte cmd2)
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            // Gửi Frame Control 5 byte: Header - Type - 0x84(T) - cmd2(S/E) - Footer
            byte[] frame = new byte[5];
            frame[0] = 0x40;        // Header '@'
            frame[1] = 0x04;        // Type 0x04
            frame[2] = 0x84;        // Ký hiệu 'T' (Test Command)
            frame[3] = cmd2;        // Lệnh S (0x83) hoặc E (0x69)
            frame[4] = 0x26;        // Footer '&'

            serialPort.Write(frame, 0, frame.Length);
            if (txtSend != null) txtSend.Text = BitConverter.ToString(frame);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // Kịch bản B: lệnh Reset thuật toán, ký tự 'R' (0x52)
            SendCommand('R');

            // Xóa số liệu cũ trên giao diện
            chartPPG.Series[0].Points.Clear();
            ppgDc = 0;
            ClearStats();
            txtBPM.Text = "--";
            txtSpO2.Text = "--";
            btnExport.Enabled = false;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            // 1. Kiểm tra xem có điểm sóng nào trên đồ thị chưa
            if (chartPPG.Series.Count == 0 || chartPPG.Series[0].Points.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu sóng PPG để xuất!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Khởi tạo hộp thoại lưu file bằng code
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV File (*.csv)|*.csv|Text File (*.txt)|*.txt";
                saveFileDialog.Title = "Xuất dữ liệu đo MAX30102";
                saveFileDialog.FileName = $"PPG_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(saveFileDialog.FileName))
                        {
                            // Ghi thông tin chỉ số
                            sw.WriteLine("=== MAX30102 HEALTH MONITOR REPORT ===");
                            sw.WriteLine($"Export Time,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            sw.WriteLine($"BPM Average,{txtBPM_Avg.Text}");
                            sw.WriteLine($"BPM Min / Max,{txtBPM_Min.Text} / {txtBPM_Max.Text}");
                            sw.WriteLine($"SpO2 Average,{txtSpO2_Avg.Text}");
                            sw.WriteLine($"SpO2 Min / Max,{txtSpO2_Min.Text} / {txtSpO2_Max.Text}");
                            sw.WriteLine();

                            // Ghi tọa độ sóng PPG
                            sw.WriteLine("=== PPG WAVEFORM DATA ===");
                            sw.WriteLine("Sample_Index,PPG_Amplitude");

                            int sampleIndex = 1;
                            foreach (var point in chartPPG.Series[0].Points)
                            {
                                sw.WriteLine($"{sampleIndex++},{point.YValues[0]}");
                            }
                        }

                        MessageBox.Show("Xuất file thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi xuất file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}