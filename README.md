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
