# Measure-blood-oxygen-levels-and-heart-rate-using-the-MAX30102-sensor.
This's just a school subject's project.
## Project Overview

This project is a real-time health monitoring system designed to measure **heart rate (BPM)** and **blood oxygen saturation (SpO₂)** using the **MAX30102 sensor**.

The MAX30102 collects the raw physiological data and transmits it to an **ESP32**, where the measurements are processed and converted into meaningful heart rate and SpO₂ values. The ESP32 then sends the processed data to an **Altera DE2 FPGA development board** using a predefined data frame protocol.

The DE2 receives and processes the data before transmitting it to a **C# GUI application** running on a laptop through a **COM (serial) connection**. The GUI displays the real-time measurements and provides live-time charts for both heart rate and blood oxygen saturation.

The GUI also supports sending control commands to the DE2, such as stopping the measurement process. In addition, measured data can be exported and stored as a **CSV file** for further analysis and record keeping.

### System Architecture

**MAX30102 → ESP32 → Altera DE2 → C# GUI → CSV**

### Main Features

* Real-time heart rate (BPM) measurement
* Real-time blood oxygen saturation (SpO₂) measurement
* MAX30102 sensor data acquisition
* Data processing on ESP32
* Communication between ESP32 and Altera DE2
* Predefined data frame protocol
* Serial communication between DE2 and PC via COM port
* Real-time data visualization using C# GUI
* Live heart rate and SpO₂ charts
* Control commands from GUI to DE2
* Measurement data export to CSV
---

## User Guide

### 1. Installation

1. Run `MAX30102_Setup.exe`.
2. Follow the installation wizard and grant Administrator permission if requested by Windows.
3. After installation, launch **MAX30102 WPF** from the Start Menu or the Desktop shortcut (if created during installation).

### 2. Connecting to the Device

1. Connect the microcontroller board to the computer via USB-UART.
2. Open the application and select the correct COM port corresponding to the device.
3. Set the Baud Rate to `9600`.
4. Click **Connect**. Once the connection is successful, the status will change to Connected.

**If the connection fails:** Check the USB cable, USB-UART driver, selected COM port, and make sure the port is not being used by another application.

### 3. Measuring and Monitoring Data

- **Start:** Sends the measurement start command. The application updates BPM, SpO₂, and the PPG waveform when data is received from the device.
- **Stop:** Stops the measurement process.
- **Reset:** Sends the reset command and clears the displayed statistics and graph data.
- **BPM and SpO₂:** Display the current measurement values. Avg, Min, and Max show the statistics collected during the measurement session.
- **PPG Waveform:** Displays the PPG signal. Move the mouse over the graph to inspect values. Click to pin a marker for comparison.
- **Freeze:** Temporarily pauses graph updates.
- **Clear Pins:** Removes all pinned markers from the graph.

### 4. Communication Test

1. Connect the device and make sure the firmware supports TEST packet loopback.
2. Click **Run Test** to start the test.
3. The application sends 1000 numbered TEST packets sequentially and waits for the firmware to return them.
4. Monitor the progress bar and test results. Packets that are not received back are counted as lost packets.

**Note:** The test requires firmware support for returning TEST packets according to the predefined protocol.

### 5. Data Transmission and Logging

- Open the **Send and Receive** section, enter the message in ASCII text format, and click **Send**.
- **Last Frame Received** displays the most recently received data frame.
- Open the **Log** section to view transmitted and received frames. Click **Clear** to clear the displayed log.

### 6. Data Export

1. After PPG data has been collected, click **Export**.
2. Select the destination folder and choose CSV or TXT format in the save dialog.
3. Check the exported file at the selected location.

### 7. Disconnecting and Additional Notes

- Click **Disconnect** before unplugging the device or closing the application.
- If the USB cable is accidentally disconnected or a connection error occurs, reconnect the device and select the correct COM port again.
- This software is intended for educational, experimental, and signal-monitoring purposes. It is not a substitute for medical devices or professional medical diagnosis.