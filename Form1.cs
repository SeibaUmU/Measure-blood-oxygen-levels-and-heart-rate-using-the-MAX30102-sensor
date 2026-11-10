using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
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
        }
        private void form1_Load(object sender, EventArgs e)
        {
            // Cấu hình cổng COM
            string[] ports = SerialPort.GetPortNames();
            cboPort.Items.AddRange(ports);
            if (ports.Length > 0) cboPort.SelectedIndex = 0;
            cboBaudRate.SelectedItem = "9600";

            // --- CẤU HÌNH ĐỒ THỊ PPG ---

            // 1. Xóa dữ liệu cũ và cài đặt đường vẽ
            chartPPG.Series.Clear();
            var series = chartPPG.Series.Add("PPG");
            series.ChartType = SeriesChartType.FastLine; // Tối ưu vẽ realtime
            series.Color = Color.Lime;                   // Sóng màu xanh lá y tế
            series.BorderWidth = 2;                      // Độ dày đường nét

            // 2. Tắt các nhãn số và lưới dọc gây rối mắt trên trục hoành (X)
            chartPPG.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            chartPPG.ChartAreas[0].AxisX.MajorGrid.Enabled = false;

            // 3. Tối ưu trục tung (Y) để sóng tự động co giãn bám sát biên độ
            chartPPG.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chartPPG.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray; // Lưới ngang màu nhạt
            chartPPG.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

            // 4. Tắt chú thích (Legend) vì chỉ có 1 đường duy nhất, giúp đồ thị rộng hơn
            chartPPG.Legends[0].Enabled = false;
        }
        private void SetupUIInitialState()
        {
            // Thiết lập trạng thái các nút ban đầu khi chưa Connect
            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            btnTest.Enabled = false;
            btnExport.Enabled = false;
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
                btnStart.Enabled = true;          // Enable nút Start
                btnTest.Enabled = true;           // Enable nút Test
                btnExport.Enabled = true;         // Enable nút Export
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
                btnStart.Enabled = false;        // Tắt nút Start
                if (btnStop != null) btnStop.Enabled = false;
                btnTest.Enabled = false;         // Tắt nút Test

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
        private void btnStart_Click(object sender, EventArgs e)
        {
            // Gửi lệnh Start: Ký tự '1' (Mã Hex: 0x31)
            SendCommand('1');

            // Quản lý nút bấm: Đang đo thì tắt nút Start và nút Reset, mở nút Stop
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            if (btnExport != null) btnExport.Enabled = false;

            // Nếu có tạo nút btnReset thì uncomment dòng dưới
            // btnReset.Enabled = false; 
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            // Gửi lệnh Stop: Ký tự '0' (Mã Hex: 0x30)
            SendCommand('0');

            // Quản lý nút bấm: Dừng đo thì mở lại Start, Reset và Export
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            if (btnExport != null) btnExport.Enabled = true; // Có dữ liệu mới cho Export

            // Nếu có tạo nút btnReset thì uncomment dòng dưới
            // btnReset.Enabled = true;
        }
        //private void btnReset_Click(object sender, EventArgs e)
        //{
        //    // Gửi lệnh Reset thuật toán: Ký tự 'R' (Mã Hex: 0x52)
        //    SendCommand('R');
        //}
        // --- HÀM GỬI FRAME TEST (TYPEDATA = 0x04) ---
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

            btnTest.Enabled = false;
            btnStart.Enabled = false;
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
                btnStart.Enabled = true;
                btnDisconnect.Enabled = true;
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

            this.Invoke(new Action(() =>
            {
                if (txtReceive != null) txtReceive.Text = rawHex;

                if (typeData == 0x01)
                {
                    byte bpm = frame[2];
                    byte spo2 = frame[3];
                    if (txtBPM != null) txtBPM.Text = bpm.ToString();
                    if (txtSpO2 != null) txtSpO2.Text = spo2.ToString() + "%";
                }
                else if (typeData == 0x03)
                {
                    ushort ppgRaw = (ushort)((frame[2] << 8) | frame[3]);
                    if (chartPPG.Series.Count > 0)
                    {
                        chartPPG.Series[0].Points.AddY(ppgRaw);
                        if (chartPPG.Series[0].Points.Count > 200) chartPPG.Series[0].Points.RemoveAt(0);
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
    }
}