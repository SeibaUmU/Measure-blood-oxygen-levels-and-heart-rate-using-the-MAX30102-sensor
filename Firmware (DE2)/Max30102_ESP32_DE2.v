module Max30102_ESP32_DE2(
    input         CLOCK_50,
    input  [3:0]  SW,
    output [17:0] LEDR,
    // Cổng COM của DE2 <-> MAX3232 <-> PCB (ESP32-S3 <-> Type-C <-> Laptop GUI)
    output        UART_TXD,   // DE2 -> PCB: gửi lệnh B (cho ESP32) và gửi A, C, Echo 0x04 (về GUI)
    input         UART_RXD,   // PCB -> DE2: nhận lệnh B, frame test 0x04 (từ GUI) và frame A, C (từ ESP32)
    // Giữ lại port GPIO để không lỗi Pin Planner trong Quartus (không còn sử dụng)
    input         GPIO_RX,
    output        GPIO_TX,
    output [6:0]  HEX0, HEX1, HEX2, HEX3, HEX4, HEX5, HEX6, HEX7
);
    wire rst_n = ~SW[0]; //RS tich cuc muc cao
    assign GPIO_TX = 1'b1; // Giữ mức nghỉ (Idle) cho chân GPIO_TX

    // =====================================================================
    // 1. KHỐI NHẬN UART DUY NHẤT QUA CỔNG COM (UART_RXD - 9600 bps)
    //Khối này đọc chân UART_RXD và xuất ra rx_byte, cờ rx_valid, cùng 2 cờ báo lỗi khung truyền (framing_error) và lỗi chẵn lẻ (parity_error).
    // =====================================================================
    wire [7:0]  rx_byte; // Dữ liệu 1 byte nhận được từ UART_RXD
    wire        rx_valid; // xung 0->1 trong 1 chu kỳ xung khi nhận được 1 byte hợp lệ

   
	wire framing_err, parity_err; // Cờ báo lỗi Framing Error (Stop bit) và Parity Error (nếu có)
    uart_rx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_rx_com (
        .clk(CLOCK_50), .rst_n(rst_n), .rx(UART_RXD),
        .data_out(rx_byte), .data_valid(rx_valid),
        .framing_error(framing_err), .parity_error(parity_err)
    );

    // =====================================================================
    // 2. GIẢI MÃ DỮ LIỆU CẢM BIẾN TỪ ESP32 (Kịch bản A: 0x01, Kịch bản C: 0x03)
    // Khối này bắt các byte từ rx_byte. Nếu nhận được gói Kịch bản A (0x01), nó xuất ra nhịp tim (hr_value), SpO2 (spo2_value) và cờ new_data. Nếu là gói Kịch bản C (0x03), nó xuất ra tín hiệu đồ thị PPG (ppg_value) 16-bit và cờ new_ppg
    // =====================================================================
    wire [7:0]  hr_value, spo2_value;
    wire        new_data;
    wire [15:0] ppg_value;
    wire        new_ppg;
    wire        clr_vals; // xung 1 chu kỳ khi nhận được lệnh '1' (Start) hoặc 'R' (Reset) từ GUI, dùng để xóa giá trị cũ trên LED 7 đoạn

    frame_decoder u_frame_decoder (
        .clk(CLOCK_50), .rst_n(rst_n), .clear(clr_vals),
        .byte_in(rx_byte), .byte_valid(rx_valid),
        .hr_value(hr_value), .spo2_value(spo2_value), .new_data(new_data),
        .ppg_value(ppg_value), .new_ppg(new_ppg)
    );

    hex_display u_hex_display (
        .hr_value(hr_value), .spo2_value(spo2_value),
        .HEX7(HEX7), .HEX6(HEX6), .HEX5(HEX5), .HEX4(HEX4),
        .HEX3(HEX3), .HEX2(HEX2), .HEX1(HEX1), .HEX0(HEX0)
    );

    // =====================================================================
    // 3. GIẢI MÃ LỆNH B TỪ GUI (Kịch bản B: 0x02)
    //Đọc dữ liệu UART để dò tìm Kịch bản B (0x02 - Lệnh từ máy tính). Khi giải mã thành công, nó xuất mã lệnh ra cmd_code kèm cờ cmd_valid
    // =====================================================================
    wire [7:0] cmd_code;
    wire       cmd_valid;
    //chạy song song với frame_decoder ở Khối 2, chịu trách nhiệm rình các byte đi vào để bắt gói tin mang định danh 0x02
    cmd_decoder u_cmd_decoder (
        .clk(CLOCK_50), .rst_n(rst_n),
        .byte_in(rx_byte), .byte_valid(rx_valid),
        .cmd_code(cmd_code), .cmd_valid(cmd_valid)
    );

    // Trạng thái đo: '1' = Start, '0' = Stop. Chỉ khi đang đo DE2 mới đẩy A/C lên GUI.
    reg running;
    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) running <= 0;
        else if (cmd_valid) begin
            if (cmd_code == 8'h31) running <= 1; // ki tu 1
            else if (cmd_code == 8'h30) running <= 0; //ki tu 0
        end
    end
    // Start ('1') hoặc Reset ('R') thì xóa HR/SpO2 cũ trên DE2
    assign clr_vals = cmd_valid && (cmd_code == 8'h31 || cmd_code == 8'h52 || cmd_code == 8'h53); //Tạo xung xóa số liệu cũ trên LED 7 đoạn nếu nhận được lệnh

    // =====================================================================
    // 4. KHỐI TEST MODE (Frame type 0x04 từ GUI)
    // =====================================================================
    reg test_mode_active;
    reg [2:0] test_rx_state; // FSM nhận frame test 0x04 từ GUI: [@][0x04][DATA_H][DATA_L][&][CRC]
    reg test_req_echo; // xung 1 chu kỳ khi nhận được frame test hợp lệ, yêu cầu phát lại về GUI
    reg [7:0] test_byte1, test_byte2; // lưu tạm thời 2 byte dữ liệu thô (DATA_H và DATA_L) đang trên đường đi vào từ gói nhận để đợi kiểm tra Footer
    reg [7:0] test_data_high, test_data_low; // 2 byte dữ liệu thô cuối cùng đã được xác nhận hợp lệ, sẵn sàng phát lại về GUI  

    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            test_mode_active <= 0;
            test_rx_state <= 0;
            test_req_echo <= 0;
            test_data_high <= 0;
            test_data_low <= 0;
            test_byte1 <= 0;
            test_byte2 <= 0;
        end else begin
            test_req_echo <= 0;
            if (rx_valid) begin
                case (test_rx_state)
                    0: if (rx_byte == 8'h40) test_rx_state <= 1;            // Header '@'
                    1: if (rx_byte == 8'h04) test_rx_state <= 2;    // Type 0x04 (Test Mode)
                       else test_rx_state <= (rx_byte == 8'h40) ? 1 : 0; 
                    2: begin test_byte1 <= rx_byte; test_rx_state <= 3; end //Lưu 2 byte dữ liệu vào test_byte1 và test_byte2
                    3: begin test_byte2 <= rx_byte; test_rx_state <= 4; end
                    4: begin
                            if (rx_byte == 8'h26) begin // Footer '&'
                                if ((test_byte1 == 8'h84 && test_byte2 == 8'h83) ||
                                    (test_byte1 == 8'h54 && test_byte2 == 8'h53)) //Nhận 0x84 0x83 (T S) hoặc 0x54 0x53 (T S): Bật chế độ Test
                                    test_mode_active <= 1;
                                else if ((test_byte1 == 8'h84 && test_byte2 == 8'h69) ||
                                            (test_byte1 == 8'h54 && test_byte2 == 8'h45)) //Nhận 0x84 0x69 (T E): Tắt chế độ Test
                                    test_mode_active <= 0;
                                else if (test_mode_active) begin // Nếu đang ở chế độ Test Mode, thì nhận được frame 0x04 hợp lệ từ GUI sẽ phát lại về GUI
                                    test_data_high <= test_byte1;
                                    test_data_low  <= test_byte2;
                                    test_req_echo  <= 1;
                                end
                            end
                            test_rx_state <= 0;
                        end
                    default: test_rx_state <= 0;
                endcase
            end
        end
    end

    // =====================================================================
    // 5. PHÁT QUA CỔNG COM (UART_TXD):
    //    - Chuyển tiếp Lệnh B (0x02 - 5 byte có CRC) xuống điều khiển ESP32
    //    - Phát Frame A (0x01 - 6 byte), Frame C (0x03 - 6 byte), Echo Test (0x04 - 5 byte) về GUI
    // =====================================================================
    wire       com_busy; // Cờ báo bận khi đang phát 1 byte ra UART_TXD
    reg        com_req; // xung 1 chu kỳ yêu cầu phát 1 byte ra UART_TXD
    reg  [7:0] com_data; // Dữ liệu 1 byte cần phát ra UART_TXD

    uart_tx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_tx_com (
        .clk(CLOCK_50), .rst_n(rst_n), .tx_req(com_req),
        .tx_data(com_data), .tx_pin(UART_TXD), .tx_busy(com_busy)
    );

    reg [24:0] a_timer;              // 0.5 s gửi lại frame A một lần
    reg        pend_b, pend_a, pend_c, pend_t; // cờ yêu cầu phát lệnh B, frame A, frame C, frame test 0x04
    reg [7:0]  b_cmd;                // mã lệnh B cần chuyển tiếp cho ESP32
    reg [7:0]  c_hi, c_lo;           // mẫu PPG mới nhất
    reg [7:0]  f_type, f_d1, f_d2;   // frame đang phát
    reg [2:0]  idx; // Chỉ số byte đang phát (0..5)
    reg [1:0]  tx_st; // FSM phát từng byte ra UART_TXD: 0=chờ phát, 1=phát byte, 2=chờ busy, 3=chờ xong
    reg [7:0]  cur_byte; // byte hiện tại đang phát ra UART_TXD
    reg        wait_s;               // đang chờ kết quả đo 1 lần
    reg [29:0] s_timer;              // timeout phía DE2
    wire [7:0] f_crc_ac  = f_type ^ f_d1 ^ f_d2 ^ 8'h26; // CRC cho frame 6 byte (0x01, 0x03)
    wire [7:0] f_crc_b   = 8'h02  ^ f_d1 ^ 8'h26;        // CRC cho frame 5 byte lệnh B (0x02)
    wire       last_byte = (f_type == 8'h02 || f_type == 8'h04) ? (idx == 3'd4) : (idx == 3'd5); // Cờ chỉ ra byte cuối cùng trong frame

    always @(*) begin  //tự động nội suy xem tại chu kỳ (idx) hiện tại, byte cần gửi đi là byte gì (cur_byte)
        if (f_type == 8'h02) begin
            // Cấu trúc gói 5 byte Lệnh B: [@][0x02][CMD][&][CRC]
            case (idx)
                3'd0:    cur_byte = 8'h40;
                3'd1:    cur_byte = 8'h02;
                3'd2:    cur_byte = f_d1;
                3'd3:    cur_byte = 8'h26;
                default: cur_byte = f_crc_b;
            endcase
        end else begin
            // Cấu trúc gói A/C (6 byte) và gói Test 0x04 (5 byte, dừng ở idx=4)
            case (idx)
                3'd0:    cur_byte = 8'h40;
                3'd1:    cur_byte = f_type;
                3'd2:    cur_byte = f_d1;
                3'd3:    cur_byte = f_d2;
                3'd4:    cur_byte = 8'h26;
                default: cur_byte = f_crc_ac;
            endcase
        end
    end

    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            com_req <= 0; com_data <= 0; tx_st <= 0; idx <= 0; a_timer <= 0;
            pend_b <= 0; pend_a <= 0; pend_c <= 0; pend_t <= 0;
            b_cmd <= 0; c_hi <= 0; c_lo <= 0;
            f_type <= 0; f_d1 <= 0; f_d2 <= 0;
            wait_s <= 0; s_timer <= 0;
        end else begin
            com_req <= 0;

            // --- Ghi nhận yêu cầu phát ---
            
            if (cmd_valid) begin
                b_cmd  <= cmd_code;
                pend_b <= 1; // Ưu tiên phát lệnh B xuống ESP32
            end

            if (test_mode_active) begin
                if (test_req_echo) pend_t <= 1;
            end else if (running) begin //Bật pend_c mỗi khi có mẫu PPG mới, và dùng biến đếm a_timer để tự động bật pend_a đẩy kết quả nhịp tim mỗi 0.5 giây
                if (new_ppg) begin c_hi <= ppg_value[15:8]; c_lo <= ppg_value[7:0]; pend_c <= 1; end
                if (a_timer < 25_000_000) a_timer <= a_timer + 1; // Nếu chưa đủ 0.5 giây thì tăng a_timer lên 1
                else begin a_timer <= 0; pend_a <= 1; end
            end else begin
                a_timer <= 0;
                pend_a  <= 0;
                pend_c  <= 0;
            end
            // --- Lệnh 'S': đo 1 lần, chờ ESP32 trả kết quả hợp lệ rồi đẩy lên GUI ---
            if (cmd_valid && cmd_code == 8'h53 && !running) begin
                wait_s  <= 1;
                s_timer <= 0;
            end else if (wait_s) begin
                if (new_data && hr_value != 0 && spo2_value != 0) begin
                    wait_s <= 0;
                    pend_a <= 1;                       // hr/spo2 đã là giá trị mới
                end else if (s_timer == 900_000_000) begin   // 18 s > 15 s của ESP32
                    wait_s <= 0;
                    pend_a <= 1;                       // gửi 0,0 (đã bị xóa bởi clr_vals)
                end else s_timer <= s_timer + 1;
            end
            // --- FSM phát từng byte ra UART_TXD ---
            case (tx_st)
                //case 0: mach ranh roi
                0: begin //Kiểm tra lần lượt các cờ ưu tiên (B -> Test -> A -> C), nạp loại khung truyền (f_type), dữ liệu (f_d1, f_d2), reset idx về 0 và chuyển sang phát
                    idx <= 0; 
                    if (pend_b) begin
                        f_type <= 8'h02; f_d1 <= b_cmd; f_d2 <= 8'h00;
                        pend_b <= 0; tx_st <= 1;
                    end else if (pend_t) begin
                        f_type <= 8'h04; f_d1 <= test_data_high; f_d2 <= test_data_low;
                        pend_t <= 0; tx_st <= 1;
                    end else if (pend_a) begin
                        f_type <= 8'h01; f_d1 <= hr_value; f_d2 <= spo2_value;
                        pend_a <= 0; tx_st <= 1;
                    end else if (pend_c) begin
                        f_type <= 8'h03; f_d1 <= c_hi; f_d2 <= c_lo;
                        pend_c <= 0; tx_st <= 1;
                    end
                end 
                //Đưa cur_byte vào module uart_tx, kích xung com_req, chờ cờ com_busy báo bận, sau đó tăng idx lên để gửi byte tiếp theo cho đến byte cuối (last_byte) thì quay về trạng thái 0
                1: if (!com_busy) begin com_data <= cur_byte; com_req <= 1; tx_st <= 2; end
                2: if (com_busy) tx_st <= 3;
                3: if (!com_busy) begin
                       if (last_byte) tx_st <= 0;
                       else begin idx <= idx + 1; tx_st <= 1; end
                   end
            endcase
        end
    end

    // =====================================================================
    // 6. LED BÁO TRẠNG THÁI
    // =====================================================================
    reg led_toggle;
    //Lật trạng thái điện áp mỗi khi bắt được một mẫu cảm biến mới, tạo hiệu ứng nhấp nháy liên tục cho LEDR[0]
    always @(posedge CLOCK_50) if (new_ppg || new_data) led_toggle <= ~led_toggle;

    reg [23:0] led_com_timer, led_cmd_timer;
    /*
    Các bộ đếm ngược (countdown) chạy ở tốc độ 50MHz. 
    Bất cứ khi nào nhận được byte mới hoặc lệnh mới, 
    bộ đếm nạp giá trị 10_000_000 (tương đương 0.2 giây). 
    Khi lớn hơn 0, LED tương ứng sẽ sáng, giúp mắt người nhìn rõ 
    được tín hiệu nháy rất nhanh của UART
    */
    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            led_com_timer <= 0; led_cmd_timer <= 0;
        end else begin
            if (rx_valid)                 led_com_timer <= 10_000_000;
            else if (led_com_timer > 0)   led_com_timer <= led_com_timer - 1;
            if (cmd_valid || test_req_echo) led_cmd_timer <= 10_000_000;
            else if (led_cmd_timer > 0)   led_cmd_timer <= led_cmd_timer - 1;
        end
    end

    assign LEDR[0]    = led_toggle;               // Nhấp nháy khi nhận được gói đo từ ESP32
    assign LEDR[1]    = running;                  // Sáng khi đang ở trạng thái đo (Start)
    assign LEDR[2]    = test_mode_active;         // Sáng khi đang ở chế độ Test Mode
    assign LEDR[3]   = (led_cmd_timer > 0);      // Sáng khi nhận được lệnh từ GUI
    assign LEDR[4]   = (led_com_timer > 0);      // Sáng khi có dữ liệu vào cổng COM
	assign LEDR[15:5] = 11'd0;
    assign LEDR[16]   = framing_err; // Sáng khi phát hiện lỗi Framing Error (Stop bit)
    assign LEDR[17]   = parity_err;  // Sáng khi phát hiện lỗi Parity Error
endmodule