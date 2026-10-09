#include <Wire.h>
#include "MAX30105.h"
#include "spo2_algorithm.h"

#define I2C_SDA 21
#define I2C_SCL 22
#define UART2_TX 17     // ESP32 -> MAX3232 -> COM DE2 (frame A, C)
#define UART2_RX 16     // COM DE2 -> MAX3232 -> ESP32 (lệnh kịch bản B)
#define UART2_BAUD 9600

MAX30105 particleSensor;

#define BUFFER_LENGTH 100
uint32_t irBuffer[BUFFER_LENGTH];
uint32_t redBuffer[BUFFER_LENGTH];

int32_t spo2;
int8_t validSPO2;
int32_t heartRate;
int8_t validHeartRate;

bool measuring = false;   // false = đang dừng, chờ lệnh Start ('1') từ GUI
bool resetFlag = false;   // true = xóa dữ liệu cũ và đo lại từ đầu

// Hàm gửi Kịch bản A (Kết quả)
void sendFrameA(uint8_t hr, uint8_t spo2) {
  uint8_t header = 0x40; // '@'
  uint8_t type = 0x01;
  uint8_t footer = 0x26; // '&'
  uint8_t crc = type ^ hr ^ spo2 ^ footer;

  Serial2.write(header);
  Serial2.write(type);
  Serial2.write(hr);
  Serial2.write(spo2);
  Serial2.write(footer);
  Serial2.write(crc);
}

// Hàm gửi Kịch bản C (Dữ liệu thô PPG)
void sendFrameC(uint16_t ppg_raw) {
  uint8_t header = 0x40; // '@'
  uint8_t type = 0x03;
  uint8_t highByte = (ppg_raw >> 8) & 0xFF;
  uint8_t lowByte = ppg_raw & 0xFF;
  uint8_t footer = 0x26; // '&'

  uint8_t crc = type ^ highByte ^ lowByte ^ footer;

  Serial2.write(header);
  Serial2.write(type);
  Serial2.write(highByte);
  Serial2.write(lowByte);
  Serial2.write(footer);
  Serial2.write(crc);
}

// Kịch bản B: thực thi lệnh nhận từ GUI (qua DE2)
void applyCommand(uint8_t cmd) {
  if (cmd == '1') {            // 0x31: Bắt đầu đo
    measuring = true;
    resetFlag = true;          // bắt đầu với dữ liệu mới
  } else if (cmd == '0') {     // 0x30: Dừng đo
    measuring = false;
  } else if (cmd == 'R') {     // 0x52: Reset thuật toán
    resetFlag = true;
  } else {
    return;
  }
  Serial.print("CMD: ");
  Serial.println((char)cmd);
}

// Kịch bản B: nhận frame [@][0x02][CMD][&][CRC], CRC = 0x02 ^ CMD ^ 0x26
void pollCommand() {
  static uint8_t state = 0;
  static uint8_t cmd = 0;

  while (Serial2.available()) {
    uint8_t b = Serial2.read();
    switch (state) {
      case 0: if (b == 0x40) state = 1; break;
      case 1: if (b == 0x02) state = 2; else state = (b == 0x40) ? 1 : 0; break;
      case 2: cmd = b; state = 3; break;
      case 3: state = (b == 0x26) ? 4 : 0; break;
      case 4:
        if (b == (uint8_t)(0x02 ^ cmd ^ 0x26)) applyCommand(cmd);
        state = 0;
        break;
    }
  }
}

// Đọc 1 mẫu vào buffer[i] và gửi Kịch bản C. Trả về false nếu bị Stop/Reset giữa chừng.
bool grabSample(byte i) {
  while (true) {
    pollCommand();
    if (!measuring || resetFlag) return false;
    if (particleSensor.available()) break;
    particleSensor.check();
  }

  redBuffer[i] = particleSensor.getRed();
  irBuffer[i] = particleSensor.getIR();

  // Rút gọn giá trị IR (18-bit) xuống 16-bit để nhét vừa frame gửi lên GUI vẽ đồ thị
  uint16_t ppg_send = (uint16_t)(irBuffer[i] >> 2);
  sendFrameC(ppg_send);

  particleSensor.nextSample();
  return true;
}

void setup() {
  Serial.begin(115200);
  Serial2.begin(UART2_BAUD, SERIAL_8N1, UART2_RX, UART2_TX);
  Wire.begin(I2C_SDA, I2C_SCL);

  if (!particleSensor.begin(Wire, I2C_SPEED_FAST)) {
    Serial.println("Khong tim thay MAX30102!");
    while (1);
  }

  byte ledBrightness = 60;
  byte sampleAverage = 4;
  byte ledMode = 2;
  int sampleRate = 100;
  int pulseWidth = 411;
  int adcRange = 4096;

  particleSensor.setup(ledBrightness, sampleAverage, ledMode, sampleRate, pulseWidth, adcRange);
}

void loop() {
  pollCommand();

  if (resetFlag) {                       // Start hoặc Reset: bỏ mẫu cũ, đo lại từ đầu
    resetFlag = false;
    particleSensor.clearFIFO();
    while (particleSensor.available()) particleSensor.nextSample();
    spo2 = 0;
    heartRate = 0;
  }

  if (!measuring) {                      // Đang dừng: không gửi gì lên DE2
    delay(5);
    return;
  }

  particleSensor.check();
  long irValue = particleSensor.getIR();

  if (irValue < 50000) {
    sendFrameA(0, 0);
    delay(100);
    return;
  }

  for (byte i = 0; i < BUFFER_LENGTH; i++) {
    if (!grabSample(i)) return;          // bị Stop/Reset -> thoát, loop() chạy lại từ đầu
  }

  maxim_heart_rate_and_oxygen_saturation(irBuffer, BUFFER_LENGTH, redBuffer,
                                          &spo2, &validSPO2, &heartRate, &validHeartRate);

  while (true) {
    for (byte i = 25; i < BUFFER_LENGTH; i++) {
      redBuffer[i - 25] = redBuffer[i];
      irBuffer[i - 25] = irBuffer[i];
    }

    for (byte i = 75; i < BUFFER_LENGTH; i++) {
      if (!grabSample(i)) return;
    }

    maxim_heart_rate_and_oxygen_saturation(irBuffer, BUFFER_LENGTH, redBuffer,
                                            &spo2, &validSPO2, &heartRate, &validHeartRate);

    sendFrameA((uint8_t)constrain(heartRate, 0, 255), (uint8_t)constrain(spo2, 0, 255));
  }
}