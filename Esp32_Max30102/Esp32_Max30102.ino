#include <Arduino.h>
#include <Wire.h>
#include "spo2_algorithm.h"

// ===================================== PIN MAP  =====================================
static const int PIN_STATUS_LED = 10;
static const int PIN_BOOT_BTN = 0;
static const int PIN_I2C_SDA = 8;
static const int PIN_I2C_SCL = 9;
static const int PIN_U1_TX = 17;
static const int PIN_U1_RX = 16;

#define UART_BAUD 9600

// UART1 RS232
HardwareSerial RS232(1); // Khởi tạo đối tượng phần cứng điều khiển UART thứ 1
bool wireStarted = false;
bool sensorOk = false;

// ===================================== THANH GHI MAX30102  =====================================
static const uint8_t MAX_ADDR = 0x57; // Địa chỉ I2C của MAX30102 (7-bit)
static const uint8_t REG_INTR_EN1 = 0x02;
static const uint8_t REG_INTR_EN2 = 0x03;
static const uint8_t REG_FIFO_WR = 0x04;
static const uint8_t REG_FIFO_OVF = 0x05;
static const uint8_t REG_FIFO_RD = 0x06;
static const uint8_t REG_FIFO_DATA = 0x07;
static const uint8_t REG_FIFO_CFG = 0x08;
static const uint8_t REG_MODE_CFG = 0x09;
static const uint8_t REG_SPO2_CFG = 0x0A;
static const uint8_t REG_LED1_PA = 0x0C; // RED
static const uint8_t REG_LED2_PA = 0x0D; // IR
static const uint8_t REG_REV_ID = 0xFE;
static const uint8_t REG_PART_ID = 0xFF;

static const uint8_t MAX_LED_PA = 0x24;  // ~7.2 mA
static const uint32_t FINGER_TH = 50000; // IR > 50000 = có ngón tay

// ===================================== BỘ ĐỆM & BIẾN THUẬT TOÁN ESP32.INO =====================================
#define BUFFER_LENGTH 100
uint32_t irBuffer[BUFFER_LENGTH];  // Lưu 100 mẫu IR (PPG) để tính nhịp tim
uint32_t redBuffer[BUFFER_LENGTH]; // Lưu 100 mẫu RED (PPG) để tính SpO2
uint32_t tsBuffer[BUFFER_LENGTH];  // micros() lúc nhận từng mẫu, để tính toán thời gian giữa các đỉnh sóng
int bufCount = 0;                  // Số mẫu hiện có trong buffer (0..100)
int newSampleCnt = 0;              // Đếm đủ 25 mẫu mới thì tính lại BPM/SpO2

float bpmAvg = 0;     // Nhịp tim đã làm mượt
int32_t spo2 = 0;     // int32_t 32 bit co dau
int8_t validSPO2 = 0; // 1 = SpO2 hợp lệ, 0 = không hợp lệ
int32_t heartRate = 0;
int8_t validHeartRate = 0;

int32_t lastSpo2 = 0; // SpO2 hợp lệ gần nhất từ thư viện Maxim (0 = chưa có)

bool measuring = false; // false = chờ lệnh Start ('1') từ DE2
bool resetFlag = false;
bool oneShot = false; // true = đo 1 lần (FW-12)
uint32_t oneShotStart = 0;
// ===================================== DRIVER MAX30102 =====================================
void startWire() // Khởi tạo I2C (Wire) với tốc độ 400kHz
{
  Wire.begin(PIN_I2C_SDA, PIN_I2C_SCL, 400000);
  wireStarted = true;
}

bool maxWrite(uint8_t reg, uint8_t val) // hàm ghi 1 byte val vào thanh ghi reg của MAX30102 qua I2C, trả về true nếu thành công
{
  Wire.beginTransmission(MAX_ADDR);
  Wire.write(reg);
  Wire.write(val);
  return Wire.endTransmission() == 0;
}

bool maxRead(uint8_t reg, uint8_t *buf, uint8_t len) // hàm đọc len byte từ thanh ghi reg của MAX30102 qua I2C, lưu vào buf, trả về true nếu thành công
{
  Wire.beginTransmission(MAX_ADDR);
  Wire.write(reg);
  if (Wire.endTransmission(false) != 0)
    return false;
  uint8_t n = Wire.requestFrom((uint8_t)MAX_ADDR, len);
  if (n != len)
    return false;
  for (uint8_t i = 0; i < len; i++)
    buf[i] = Wire.read();
  return true;
}

int maxRead8(uint8_t reg) // hàm đọc 1 byte từ thanh ghi reg của MAX30102 qua I2C, trả về giá trị byte đó hoặc -1 nếu lỗi
{
  uint8_t v;
  return maxRead(reg, &v, 1) ? v : -1;
}

void maxClearFifo()
{
  maxWrite(REG_FIFO_WR, 0);
  maxWrite(REG_FIFO_OVF, 0);
  maxWrite(REG_FIFO_RD, 0);
}

bool maxInit() // khởi tạo MAX30102, trả về true nếu thành công
{
  if (!maxWrite(REG_MODE_CFG, 0x40)) // 0x40 = soft reset
    return false;                    // soft reset
  uint32_t t = millis();
  while (millis() - t < 100)
  {
    int m = maxRead8(REG_MODE_CFG);
    if (m >= 0 && !(m & 0x40))
      break;
    delay(2);
  }
  bool ok = true;
  ok &= maxWrite(REG_INTR_EN1, 0x00); // INT không nối về MCU
  ok &= maxWrite(REG_INTR_EN2, 0x00);
  ok &= maxWrite(REG_FIFO_CFG, 0x5F); // SMP_AVE=4, rollover, A_FULL=15 -> 100/4 = 25 sps (khớp FreqS=25 của thư viện Maxim)
  ok &= maxWrite(REG_SPO2_CFG, 0x27); // ADC 4096nA, 100sps, 411us/18bit
  ok &= maxWrite(REG_LED1_PA, MAX_LED_PA);
  ok &= maxWrite(REG_LED2_PA, MAX_LED_PA);
  ok &= maxWrite(REG_MODE_CFG, 0x03); // SpO2 mode (RED + IR)
  maxClearFifo();
  return ok;
}

int maxAvailable()
{
  int wr = maxRead8(REG_FIFO_WR), rd = maxRead8(REG_FIFO_RD);
  if (wr < 0 || rd < 0)
    return 0;
  return (wr - rd) & 0x1F;
}

bool maxReadSample(uint32_t &red, uint32_t &ir)
{
  uint8_t b[6];
  if (!maxRead(REG_FIFO_DATA, b, 6))
    return false;
  red = (((uint32_t)b[0] << 16) | ((uint32_t)b[1] << 8) | b[2]) & 0x3FFFF;
  ir = (((uint32_t)b[3] << 16) | ((uint32_t)b[4] << 8) | b[5]) & 0x3FFFF;
  return true;
}

// Quy trình quét I2C + khởi tạo MAX30102
bool initSensorPhiStyle()
{
  if (wireStarted)
  {
    Wire.end();
    wireStarted = false;
  }
  pinMode(PIN_I2C_SDA, INPUT);
  pinMode(PIN_I2C_SCL, INPUT);
  delay(5);
  startWire();

  bool hasMax = false;
  for (uint8_t a = 1; a < 127; a++)
  {
    Wire.beginTransmission(a);
    if (Wire.endTransmission() == 0)
    {
      if (a == MAX_ADDR)
        hasMax = true;
    }
  }
  if (!hasMax)
    return false;
  if (maxRead8(REG_PART_ID) != 0x15)
    return false;
  return maxInit();
}

// ===================================== GIAO THỨC KHUNG TRUYỀN & CẦU NỐI UART =====================================
void sendFrameA(uint8_t hr, uint8_t spo2_val) // so nguyen ko dau uint8_t
{
  uint8_t buf[6]; // 6 byte: @ + type + hr + spo2 + & + crc
  buf[0] = 0x40;  // '@'
  buf[1] = 0x01;  // dinh nghia la goi tin ket qua do (kich ban A)
  buf[2] = hr;
  buf[3] = spo2_val;
  buf[4] = 0x26;                              // '&'
  buf[5] = buf[1] ^ buf[2] ^ buf[3] ^ buf[4]; // CRC XOR
  RS232.write(buf, 6);
}

void sendFrameC(uint16_t ppg_raw)
{
  uint8_t buf[6];
  buf[0] = 0x40;                  // '@'
  buf[1] = 0x03;                  // dinh nghia la goi tin du lieu PPG
  buf[2] = (ppg_raw >> 8) & 0xFF; // 8 bit cao
  buf[3] = ppg_raw & 0xFF;        // 8 bit thap
  buf[4] = 0x26;                  // '&'
  buf[5] = buf[1] ^ buf[2] ^ buf[3] ^ buf[4];
  RS232.write(buf, 6); // xuat tuan tu 6 byte ra tx
}

void applyCommand(uint8_t cmd)
{
  if (cmd == '1')
  { // 0x31: Bắt đầu đo
    measuring = true;
    resetFlag = true;
  }
  else if (cmd == '0')
  { // 0x30: Dừng đo
    measuring = false;
  }
  else if (cmd == 'R')
  { // 0x52: Reset thuật toán
    resetFlag = true;
  }
  else if (cmd == 'S')
  { // 0x53: Đọc 1 lần (FW-12)
    if (!measuring)
    { // đang Start liên tục thì bỏ qua
      measuring = true;
      resetFlag = true; // xóa FIFO + buffer, bpmAvg = 0
      oneShot = true;
      oneShotStart = millis();
    }
  }
}

void serviceBridge()
{
  // 1. CHIỀU ĐI: Laptop C# (Serial) -> ESP32 -> DE2 (RS232)
  static uint8_t pc_state = 0; // 0 = chờ '@', 1 = chờ type, 2 = chờ data, 3 = chờ '&', 4 = chờ CRC
  static uint8_t pc_buf[5];    // 5 byte: @ + type + data + & + crc (B va Test)

  while (Serial.available()) // Có dữ liệu từ C# gửi xuống
  {
    uint8_t b = Serial.read(); // Đọc từng byte từ Serial
    switch (pc_state)
    {
    case 0:
      if (b == 0x40) //@
      {
        pc_buf[0] = b;
        pc_state = 1;
      }
      break;
    case 1:
      if (b == 0x02 || b == 0x04) // kich ban B (0x02) hoac Test (0x04)
      {
        pc_buf[1] = b;
        pc_state = 2;
      }
      else
        pc_state = (b == 0x40) ? 1 : 0; // Nếu không phải 0x02 hoặc 0x04, reset về trạng thái chờ '@' (0) hoặc giữ nguyên nếu gặp '@'
      break;
    case 2:
      pc_buf[2] = b; // Dữ liệu (lệnh hoặc dữ liệu test)
      pc_state = 3;
      break;
    case 3:
      pc_buf[3] = b; // Dấu '&'
      pc_state = 4;
      break;
    case 4:
      pc_buf[4] = b; // CRC
      // bơm nguyên vẹn cả 5 byte gói lệnh này xuống FPGA DE2.
      // lý do phải gom đủ 5 byte rồi mới gửi là để tránh trường hợp
      // các byte của gói lệnh bị xen kẽ làm đứt quãng gói dữ liệu đồ thị/kết quả đang được ESP32 phát xuống liên tục
      RS232.write(pc_buf, 5); // Chỉ chuyển tiếp xuống DE2 (master); lệnh chỉ có hiệu lực khi DE2 echo frame B về
      pc_state = 0;
      break;
    }
  }

  // 2. CHIỀU VỀ: DE2 (RS232) -> ESP32 -> Laptop C# (Serial)
  static uint8_t rxBuf[64]; // Bộ đệm nhận dữ liệu từ DE2 (RS232), static giúp dữ liệu trong mảng không bị mất đi khi thoát hàm
  static int rxLen = 0;

  while (RS232.available() && rxLen < (int)sizeof(rxBuf))
  {
    rxBuf[rxLen++] = (uint8_t)RS232.read();
  }

  while (rxLen >= 5) // goi tin B va Test co 5 byte, goi tin A co 6 byte
  {
    if (rxBuf[0] != 0x40) // Nếu byte đầu tiên không phải '@', loại bỏ byte đó và tiếp tục kiểm tra
    {
      /*
      Cơ chế trượt khung (memmove):
      Nếu byte đầu không phải là @ (có thể do nhiễu vật lý hoặc dư âm của gói tin trước),
      hàm memmove sẽ cắt bỏ byte rác đó, kéo toàn bộ mảng dịch sang trái 1 vị trí (rxBuf + 1 đè lên rxBuf),
      giảm rxLen đi 1 và dùng continue vòng lại kiểm tra byte đầu tiên mới.
      Quá trình này lặp lại cho đến khi bộ đệm "tóm" được chính xác chữ @ ở vị trí số 0.
      */
      memmove(rxBuf, rxBuf + 1, rxLen - 1);
      rxLen--;
      continue;
    }

    uint8_t type = rxBuf[1]; // doc byte 2 de xac dinh type data
    int needLen = 0;
    if (type == 0x01 || type == 0x03) // A hoac C
      needLen = 6;
    else if (type == 0x02 || type == 0x04) // B hoac Test
      needLen = 5;
    else
    { // Nếu byte thứ 2 không phải là 0x01, 0x02, 0x03 hoặc 0x04, loại bỏ byte đó và tiếp tục kiểm tra
      memmove(rxBuf, rxBuf + 1, rxLen - 1);
      rxLen--;
      continue;
    }

    if (rxLen < needLen) // Nếu chưa đủ byte để kiểm tra, thoát vòng while và chờ thêm dữ liệu
      break;

    bool valid = false;
    if (type == 0x02) // B
    {
      uint8_t crc = 0x02 ^ rxBuf[2] ^ 0x26;
      if (rxBuf[3] == 0x26 && rxBuf[4] == crc) // byte t3 = & va byte t4 = ket qua xor crc
      {
        valid = true;
        applyCommand(rxBuf[2]); // Thực hiện lệnh từ DE2
      }
    }
    else if (type == 0x04) // Test
    {
      if (rxBuf[4] == 0x26) // rieng test ko can crc, chi can & o byte 5
      {
        valid = true;
        Serial.write(rxBuf, 5);
      }
    }
    else if (type == 0x01 || type == 0x03) // A hoac C
    {
      uint8_t crc = type ^ rxBuf[2] ^ rxBuf[3] ^ 0x26;
      if (rxBuf[4] == 0x26 && rxBuf[5] == crc)
      {
        valid = true;
        Serial.write(rxBuf, 6);
      }
    }

    if (valid)
    {
      /*
      Nếu gói tin được xử lý thành công, dùng memmove cắt bỏ toàn bộ needLen byte đó khỏi đầu mảng.
      Dữ liệu thừa của gói tin tiếp theo (nếu có) sẽ được kéo lên đầu mảng để vòng while tiếp tục xử lý lập tức.
      */
      memmove(rxBuf, rxBuf + needLen, rxLen - needLen);
      rxLen -= needLen;
    }
    else
    { // kt Header hoac Footer fail => truot 1 byte de tiep tuc kiem tra goi tin tiep theo => tranh treo
      memmove(rxBuf, rxBuf + 1, rxLen - 1);
      rxLen--;
    }
  }
}

// ===================================== THUẬT TOÁN LỌC NHỊP TIM  =====================================
float beatBpm(float fs)
{
  static float y[BUFFER_LENGTH], z[BUFFER_LENGTH]; // lưu tín hiệu sau khi loại bỏ thành phần tĩnh (DC), mảng z lưu tín hiệu sau khi làm mượt.
  const uint32_t MIN_BEAT_US = 350000UL;           // khoảng cách thời gian tối thiểu giữa 2 nhịp đập liên tiếp là 350.000 micro giây (0.35 giây), tương đương nhịp tim tối đa 171 BPM
  // Loại bỏ thành phần tĩnh (DC) bằng bộ lọc trung bình trượt (Moving Average Filter) với cửa sổ 0.5 giây
  int half = (int)(fs * 0.5f);
  if (half < 2)
    half = 2;
  for (int i = 0; i < BUFFER_LENGTH; i++)
  { // Tại mỗi vị trí i, hệ thống xác định biên giới giới hạn bên trái (a) và bên phải (b) của cửa sổ trượt.
    int a = (i - half < 0) ? 0 : i - half;
    int b = (i + half > BUFFER_LENGTH - 1) ? BUFFER_LENGTH - 1 : i + half;
    float s = 0;
    for (int k = a; k <= b; k++)
      s += irBuffer[k];
    y[i] = s / (b - a + 1) - (float)irBuffer[i];
    // s / (b - a + 1): Tính giá trị trung bình cường độ IR trong cửa sổ trượt này. Đây chính là ước lượng của đường nền DC.
    // y[i]: Lấy đường nền trung bình trừ đi giá trị tín hiệu thực tế irBuffer[i]. Kết quả lưu vào mảng y là tín hiệu chỉ chứa dao động tâm thu.
  }
  // Làm mượt tín hiệu (Lọc thông thấp 3 điểm)
  // Tại bất kỳ điểm i nào (trừ điểm đầu và cuối), giá trị mới z[i] được tính bằng trung bình của điểm đó
  // với 2 điểm liền kề. Quá trình này giúp loại bỏ nhiễu gai ngẫu nhiên (spikes).
  z[0] = y[0];
  z[BUFFER_LENGTH - 1] = y[BUFFER_LENGTH - 1];
  for (int i = 1; i < BUFFER_LENGTH - 1; i++)
    z[i] = (y[i - 1] + y[i] + y[i + 1]) / 3.0f;
  // Xác định ngưỡng (Threshold) và tìm đỉnh
  float hi = 0;
  for (int i = 0; i < BUFFER_LENGTH; i++)
    if (z[i] > hi)
      hi = z[i]; // tìm giá trị cực đại trong mảng z
  if (hi <= 0)
    return 0;
  float thr = 0.35f * hi;            // Mọi đỉnh có biên độ nhỏ hơn 35% của đỉnh cao nhất đều bị loại bỏ vì coi là nhiễu dội.
  int pk[BUFFER_LENGTH / 2], np = 0; // pk là mảng lưu vị trí index của các đỉnh tìm được, np là số lượng đỉnh đã tìm thấy.
  for (int i = 1; i < BUFFER_LENGTH - 1; i++)
  {
    // Xác định đỉnh cục bộ bằng đạo hàm: điểm z[i] được coi là đỉnh nếu nó vượt ngưỡng thr, cao hơn điểm liền trước và cao hơn hoặc bằng điểm liền sau.
    if (z[i] > thr && z[i] > z[i - 1] && z[i] >= z[i + 1])
    {
      /*
       Nếu tìm thấy một đỉnh thỏa mãn nhưng khoảng cách thời gian của nó so với đỉnh trước đó (dựa vào tsBuffer)
       quá gần, nhỏ hơn hằng số MIN_BEAT_US, thuật toán kết luận đây là cùng một nhịp đập bị dội.
       Nó sẽ so sánh và chỉ giữ lại vị trí của đỉnh cao hơn để thay thế đỉnh cũ pk[np - 1] = i.
       Nếu khoảng cách đủ an toàn, nó ghi nhận đây là một đỉnh tâm thu mới pk[np++] = i
      */
      if (np > 0 && (uint32_t)(tsBuffer[i] - tsBuffer[pk[np - 1]]) < MIN_BEAT_US)
      {
        if (z[i] > z[pk[np - 1]])
          pk[np - 1] = i;
      }
      else if (np < BUFFER_LENGTH / 2)
        pk[np++] = i;
    }
  }
  // Tính nhịp tim (BPM) dựa trên số lượng đỉnh tìm được và khoảng thời gian giữa đỉnh đầu tiên và đỉnh cuối cùng
  // Ở 25 mẫu/giây, cửa sổ 100 mẫu (4 giây) có từ 2 đỉnh trở lên
  if (np < 2)
    return 0; // Không đủ đỉnh để tính nhịp tim (ít nhất 2 đỉnh trong 4 giây, tương đương nhịp tim tối thiểu 30 BPM)
              // bpm = 60 giây * 1e6 micro giây / (thời gian giữa đỉnh cuối và đỉnh đầu) * (số nhịp tim = np - 1)
  float bpm = 60e6f * (np - 1) / (float)(uint32_t)(tsBuffer[pk[np - 1]] - tsBuffer[pk[0]]);
  if (bpm < 40.0f || bpm > 200.0f) // Nếu kết quả ra dưới 40 BPM hoặc trên 200 BPM, kết quả bị coi là bất thường
    return 0;
  return bpm;
}

// ===================================== SETUP & LOOP =====================================
void setup()
{
  Serial.begin(UART_BAUD); // type-c
  pinMode(PIN_STATUS_LED, OUTPUT);
  digitalWrite(PIN_STATUS_LED, LOW);
  pinMode(PIN_BOOT_BTN, INPUT_PULLUP); // giu nguyên trạng thái cao khi không nhấn, nhấn xuống đất (LOW) để giả lập lỗi đứt cáp cảm biến
  delay(500);                          // Chờ nguồn +3.3V và +1.8V ổn định

  RS232.setRxBufferSize(2048);                              // Tăng bộ đệm nhận RS232 lên 2KB để tránh tràn khi DE2 gửi dữ liệu liên tục
  RS232.begin(UART_BAUD, SERIAL_8N1, PIN_U1_RX, PIN_U1_TX); // Khởi tạo UART1 RS232
  delay(20);
  while (RS232.available())
    RS232.read(); // Xóa sạch bộ đệm nhận RS232 trước khi bắt đầu

  startWire(); // Khởi tạo I2C (Wire) với tốc độ 400kHz
  delay(50);

  // Quét I2C và khởi tạo MAX30102
  sensorOk = initSensorPhiStyle(); // kiểm tra xem địa chỉ I2C 0x57 và mã thiết bị tĩnh PART_ID = 0x15 của MAX30102 có phản hồi hay không
  if (sensorOk)
  {
    // Nháy LED D1 (IO10) 2 lần báo hiệu cảm biến đã sáng đèn và sẵn sàng
    for (int i = 0; i < 2; i++)
    {
      digitalWrite(PIN_STATUS_LED, HIGH);
      delay(100);
      digitalWrite(PIN_STATUS_LED, LOW);
      delay(100);
    }
  }
}

void loop()
{
  serviceBridge(); // nhận lệnh từ DE2 và C# (Serial) -> ESP32 -> DE2 (RS232) và ngược lại
  // FW-11: Kiểm tra nút BOOT (IO0) giả lập đứt cáp cảm biến HOẶC kiểm tra lỗi I2C thực tế
  bool faultInjected = (digitalRead(PIN_BOOT_BTN) == LOW);
  if (faultInjected)
  {
    sensorOk = false;
  }
  // Nếu cảm biến mất kết nối, gửi mã lỗi 0xFF lên DE2/GUI và tự thử kết nối lại mỗi 500ms (không treo)
  if (!sensorOk)
  {
    static uint32_t lastRetry = 0;
    if (millis() - lastRetry >= 500)
    {
      lastRetry = millis();
      if (measuring)
      {
        sendFrameA(0xFF, 0xFF); // Gửi mã lỗi 0xFF (ERR) lên GUI C#
        if (oneShot)
        {
          oneShot = false;
          measuring = false;
        } // báo lỗi 1 lần rồi dừng
      }
      if (!faultInjected)
      {
        sensorOk = initSensorPhiStyle(); // Tự động khôi phục khi hết lỗi
      }
    }
    return; // ep loop end sớm, ko thuc hien cac buoc tiep theo, bat dau kiem tra lai sensor tu dau
  }

  if (resetFlag)
  {
    resetFlag = false;
    maxClearFifo(); // xoa du lieu trong FIFO cua MAX30102
    bufCount = 0;
    newSampleCnt = 0;
    spo2 = 0;
    lastSpo2 = 0;
    heartRate = 0;
    bpmAvg = 0;
  }
  if (oneShot && millis() - oneShotStart > 15000)
  {
    oneShot = false;
    measuring = false; // không có ngón tay / không ra kết quả
  }
  if (!measuring)
  {
    delay(1);
    return; // Nếu chưa nhận lệnh đo từ DE2, vòng loop chỉ dừng 1ms rồi quay lại kiểm tra lệnh đo tiếp theo
  }

  // Đọc toàn bộ mẫu đang có trong FIFO theo cơ chế không chặn (Non-blocking)
  int n = maxAvailable();
  if (n == 0 && maxRead8(REG_PART_ID) != 0x15)
  {
    // Phát hiện mất phản hồi I2C từ MAX30102 khi đang chạy thật
    sensorOk = false;
    sendFrameA(0xFF, 0xFF);
    return;
  }
  for (int k = 0; k < n; k++)
  {
    uint32_t red = 0, ir = 0;
    if (!maxReadSample(red, ir)) // đọc mẫu từ FIFO MAX30102
      break;
    uint32_t nowUs = micros();

    // gửi mẫu sóng PPG (Kịch bản C - 0x03)
    sendFrameC((uint16_t)(ir >> 2));
    serviceBridge();

    // Kiểm tra ngón tay: không có ngón tay thì KHÔNG gửi frame C, chỉ gửi A(0,0)
    if (ir < FINGER_TH) // IR < 50.000 = không có ngón tay
    {
      bufCount = 0;
      newSampleCnt = 0;
      bpmAvg = 0;
      lastSpo2 = 0;
      static uint32_t lastZeroSend = 0;
      if (millis() - lastZeroSend >= 100)
      {
        lastZeroSend = millis();
        sendFrameA(0, 0);
      }
      continue; // bỏ qua các bước xử lý nhịp tim/SpO2, chỉ gửi A(0,0) mỗi 100ms
    }

    // Đưa mẫu vào bộ đệm 100 mẫu
    if (bufCount < BUFFER_LENGTH)
    {
      redBuffer[bufCount] = red;
      irBuffer[bufCount] = ir;
      tsBuffer[bufCount] = nowUs;
      bufCount++;
      if (bufCount == BUFFER_LENGTH)
      {
        newSampleCnt = 25; // Đủ 100 mẫu đầu tiên -> tính ngay lập tức
      }
    }
    else
    {
      // Dịch cửa sổ trượt sang trái 1 mẫu, thế mẫu cũ nhất bằng mẫu mới nhất
      for (byte i = 1; i < BUFFER_LENGTH; i++)
      {
        redBuffer[i - 1] = redBuffer[i];
        irBuffer[i - 1] = irBuffer[i];
        tsBuffer[i - 1] = tsBuffer[i];
      }
      redBuffer[BUFFER_LENGTH - 1] = red;
      irBuffer[BUFFER_LENGTH - 1] = ir;
      tsBuffer[BUFFER_LENGTH - 1] = nowUs;
      newSampleCnt++;
    }

    // Cứ mỗi 25 mẫu mới, chạy thuật toán lọc nhịp tim
    if (bufCount == BUFFER_LENGTH && newSampleCnt >= 25)
    {
      newSampleCnt = 0;
      // Tính nhịp tim và SpO2 thô bằng thư viện Maxim
      maxim_heart_rate_and_oxygen_saturation(irBuffer, BUFFER_LENGTH, redBuffer,
                                             &spo2, &validSPO2, &heartRate, &validHeartRate);
      serviceBridge();

      // 1. Thuật toán lọc nhịp tim beatBpm(fs) + làm mượt bpmAvg
      // fs = 100 mẫu / (thời gian giữa mẫu đầu tiên và mẫu cuối cùng) = 100 / (tsBuffer[99] - tsBuffer[0]) micro giây
      float fs = (BUFFER_LENGTH - 1) * 1e6f / (float)(uint32_t)(tsBuffer[BUFFER_LENGTH - 1] - tsBuffer[0]);
      float bpm = beatBpm(fs);
      if (bpm > 0)
      {
        bpmAvg = (bpmAvg == 0) ? bpm : 0.7f * bpmAvg + 0.3f * bpm; // 70% giá trị cũ + 30% giá trị mới
      }
      else if (validHeartRate && heartRate >= 40 && heartRate <= 200) // Nếu beatBpm() không tìm được nhịp tim nhưng thư viện Maxim vẫn cho kết quả hợp lệ, dùng kết quả đó để làm mượt
      {
        bpmAvg = (bpmAvg == 0) ? (float)heartRate : 0.7f * bpmAvg + 0.3f * (float)heartRate;
      }

      // 2. SpO2 lấy hoàn toàn từ thư viện Maxim; không hợp lệ thì giữ giá trị hợp lệ gần nhất
      if (validSPO2 && spo2 >= 70 && spo2 <= 100)
        lastSpo2 = spo2;

      uint8_t hrOut = (uint8_t)constrain((int)(bpmAvg + 0.5f), 0, 255);
      uint8_t spo2Out = (hrOut > 0) ? (uint8_t)constrain(lastSpo2, 0, 100) : 0;

      sendFrameA(hrOut, spo2Out);
      serviceBridge();
      if (oneShot && hrOut > 0 && spo2Out > 0)
      { // có kết quả đầu tiên
        oneShot = false;
        measuring = false;
        break; // thoát vòng for đọc mẫu
      }
    }
  }
}