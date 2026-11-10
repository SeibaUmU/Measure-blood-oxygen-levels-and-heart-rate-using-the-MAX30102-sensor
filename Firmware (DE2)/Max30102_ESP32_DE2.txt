module Max30102_ESP32_DE2(
    input         CLOCK_50,
    input  [3:0]  KEY,
    output [17:0] LEDR,
    // Cổng COM của DE2 <-> MAX3232 <-> ESP32
    output        UART_TXD,   // DE2 -> ESP32: chuyển tiếp lệnh kịch bản B
    input         UART_RXD,   // ESP32 -> DE2: nhận kịch bản A (kết quả), C (PPG thô)
    // GPIO của DE2 <-> USB-TTL <-> Laptop (GUI)
    input         GPIO_RX,    // GUI -> DE2: nhận kịch bản B (lệnh) và frame test 0x04
    output        GPIO_TX,    // DE2 -> GUI: gửi kịch bản A, C và echo frame test
    output [6:0]  HEX0, HEX1, HEX2, HEX3, HEX4, HEX5, HEX6, HEX7
);
    wire rst_n = KEY[0];

    // =====================================================================
    // 1. PHÍA ESP32 (cổng COM): nhận frame A và C
    // =====================================================================
    wire [7:0]  esp_byte;
    wire        esp_valid;
    wire [7:0]  hr_value, spo2_value;
    wire        new_data;
    wire [15:0] ppg_value;
    wire        new_ppg;
    wire        clr_vals;

    uart_rx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_rx_esp (
        .clk(CLOCK_50), .rst_n(rst_n), .rx(UART_RXD),
        .data_out(esp_byte), .data_valid(esp_valid)
    );

    frame_decoder u_frame_decoder (
        .clk(CLOCK_50), .rst_n(rst_n), .clear(clr_vals),
        .byte_in(esp_byte), .byte_valid(esp_valid),
        .hr_value(hr_value), .spo2_value(spo2_value), .new_data(new_data),
        .ppg_value(ppg_value), .new_ppg(new_ppg)
    );

    hex_display u_hex_display (
        .hr_value(hr_value), .spo2_value(spo2_value),
        .HEX7(HEX7), .HEX6(HEX6), .HEX5(HEX5), .HEX4(HEX4),
        .HEX3(HEX3), .HEX2(HEX2), .HEX1(HEX1), .HEX0(HEX0)
    );

    // =====================================================================
    // 2. PHÍA LAPTOP (GPIO): nhận lệnh B và frame test
    // =====================================================================
    wire [7:0] pc_byte;
    wire       pc_valid;
    wire [7:0] cmd_code;
    wire       cmd_valid;

    uart_rx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_rx_pc (
        .clk(CLOCK_50), .rst_n(rst_n), .rx(GPIO_RX),
        .data_out(pc_byte), .data_valid(pc_valid)
    );

    cmd_decoder u_cmd_decoder (
        .clk(CLOCK_50), .rst_n(rst_n),
        .byte_in(pc_byte), .byte_valid(pc_valid),
        .cmd_code(cmd_code), .cmd_valid(cmd_valid)
    );

    // Trạng thái đo: '1' = Start, '0' = Stop. Chỉ khi đang đo DE2 mới đẩy A/C lên GUI.
    reg running;
    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) running <= 0;
        else if (cmd_valid) begin
            if (cmd_code == 8'h31) running <= 1;
            else if (cmd_code == 8'h30) running <= 0;
        end
    end
    // Start hoặc Reset ('R') thì xóa HR/SpO2 cũ
    assign clr_vals = cmd_valid && (cmd_code == 8'h31 || cmd_code == 8'h52);

    // =====================================================================
    // 3. CHUYỂN TIẾP LỆNH B XUỐNG ESP32: [@][0x02][CMD][&][CRC] qua UART_TXD
    // =====================================================================
    wire       com_busy;
    reg        com_req;
    reg  [7:0] com_data;

    uart_tx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_tx_com (
        .clk(CLOCK_50), .rst_n(rst_n), .tx_req(com_req),
        .tx_data(com_data), .tx_pin(UART_TXD), .tx_busy(com_busy)
    );

    reg [7:0] fwd_cmd;
    reg [2:0] fwd_idx;
    reg [1:0] fwd_st;
    reg [7:0] fwd_byte;
    always @(*) begin
        case (fwd_idx)
            3'd0:    fwd_byte = 8'h40;
            3'd1:    fwd_byte = 8'h02;
            3'd2:    fwd_byte = fwd_cmd;
            3'd3:    fwd_byte = 8'h26;
            default: fwd_byte = 8'h02 ^ fwd_cmd ^ 8'h26;   // CRC
        endcase
    end

    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            com_req <= 0; com_data <= 0; fwd_cmd <= 0; fwd_idx <= 0; fwd_st <= 0;
        end else begin
            com_req <= 0;
            case (fwd_st)
                0: begin
                    fwd_idx <= 0;
                    if (cmd_valid) begin fwd_cmd <= cmd_code; fwd_st <= 1; end
                end
                1: if (!com_busy) begin com_data <= fwd_byte; com_req <= 1; fwd_st <= 2; end
                2: if (com_busy) fwd_st <= 3;
                3: if (!com_busy) begin
                       if (fwd_idx == 3'd4) fwd_st <= 0;           // đã gửi đủ 5 byte
                       else begin fwd_idx <= fwd_idx + 1; fwd_st <= 1; end
                   end
            endcase
        end
    end

    // =====================================================================
    // 4. KHỐI TEST MODE (frame type 0x04 từ GUI qua GPIO_RX)
    // =====================================================================
    reg test_mode_active;
    reg [2:0] test_rx_state;
    reg test_req_echo;
    reg [7:0] test_byte1, test_byte2;
    reg [7:0] test_data_high, test_data_low;

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
            if (pc_valid) begin
                case (test_rx_state)
                    0: if (pc_byte == 8'h40) test_rx_state <= 1;            // Header '@'
                    1: if (pc_byte == 8'h04) test_rx_state <= 2; else test_rx_state <= 0;
                    2: begin test_byte1 <= pc_byte; test_rx_state <= 3; end
                    3: begin test_byte2 <= pc_byte; test_rx_state <= 4; end
                    4: begin
                        if (pc_byte == 8'h26) begin                         // Footer '&'
                            if (test_byte1 == 8'h84 && test_byte2 == 8'h83) test_mode_active <= 1;
                            else if (test_byte1 == 8'h84 && test_byte2 == 8'h69) test_mode_active <= 0;
                            else if (test_mode_active) begin
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
    // 5. GỬI LÊN LAPTOP QUA GPIO_TX: frame A (0x01), C (0x03), echo test (0x04)
    //    Frame A/C: [@][type][d1][d2][&][CRC] (6 byte), frame test: 5 byte, không CRC
    // =====================================================================
    wire       gpio_busy;
    reg        gpio_req;
    reg  [7:0] gpio_data;

    uart_tx #(.CLK_FREQ(50_000_000), .BAUD_RATE(9600)) u_uart_tx_gpio (
        .clk(CLOCK_50), .rst_n(rst_n), .tx_req(gpio_req),
        .tx_data(gpio_data), .tx_pin(GPIO_TX), .tx_busy(gpio_busy)
    );

    reg [24:0] a_timer;              // 0.5 s gửi lại frame A một lần (giữ như bản cũ)
    reg        pend_a, pend_c, pend_t;
    reg [7:0]  c_hi, c_lo;           // mẫu PPG mới nhất
    reg [7:0]  f_type, f_d1, f_d2;   // frame đang gửi
    reg [2:0]  idx;
    reg [1:0]  tx_st;
    reg [7:0]  cur_byte;
    wire [7:0] f_crc     = f_type ^ f_d1 ^ f_d2 ^ 8'h26;
    wire       last_byte = (f_type == 8'h04) ? (idx == 3'd4) : (idx == 3'd5);

    always @(*) begin
        case (idx)
            3'd0:    cur_byte = 8'h40;
            3'd1:    cur_byte = f_type;
            3'd2:    cur_byte = f_d1;
            3'd3:    cur_byte = f_d2;
            3'd4:    cur_byte = 8'h26;
            default: cur_byte = f_crc;
        endcase
    end

    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            gpio_req <= 0; gpio_data <= 0; tx_st <= 0; idx <= 0; a_timer <= 0;
            pend_a <= 0; pend_c <= 0; pend_t <= 0; c_hi <= 0; c_lo <= 0;
            f_type <= 0; f_d1 <= 0; f_d2 <= 0;
        end else begin
            gpio_req <= 0;

            // --- ghi nhận yêu cầu gửi ---
            if (test_mode_active) begin
                if (test_req_echo) pend_t <= 1;
            end else if (running) begin
                if (new_ppg) begin c_hi <= ppg_value[15:8]; c_lo <= ppg_value[7:0]; pend_c <= 1; end
                if (a_timer < 25_000_000) a_timer <= a_timer + 1;
                else begin a_timer <= 0; pend_a <= 1; end
            end

            // --- FSM gửi từng byte ---
            case (tx_st)
                0: begin
                    idx <= 0;
                    if (pend_t) begin
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
                1: if (!gpio_busy) begin gpio_data <= cur_byte; gpio_req <= 1; tx_st <= 2; end
                2: if (gpio_busy) tx_st <= 3;
                3: if (!gpio_busy) begin
                       if (last_byte) tx_st <= 0;
                       else begin idx <= idx + 1; tx_st <= 1; end
                   end
            endcase
        end
    end

    // =====================================================================
    // 6. LED báo trạng thái
    // =====================================================================
    reg led_toggle;
    always @(posedge CLOCK_50) if (esp_valid) led_toggle <= ~led_toggle;

    reg [23:0] led_com_timer, led_pc_timer;
    always @(posedge CLOCK_50 or negedge rst_n) begin
        if (!rst_n) begin
            led_com_timer <= 0; led_pc_timer <= 0;
        end else begin
            if (esp_valid)                led_com_timer <= 10_000_000;
            else if (led_com_timer > 0)   led_com_timer <= led_com_timer - 1;
            if (pc_valid)                 led_pc_timer <= 10_000_000;
            else if (led_pc_timer > 0)    led_pc_timer <= led_pc_timer - 1;
        end
    end

    assign LEDR[0]    = led_toggle;
    assign LEDR[1]    = running;                  // sáng khi GUI đã nhấn Start
    assign LEDR[15:2] = 14'd0;
    assign LEDR[16]   = (led_pc_timer  > 0);      // có dữ liệu từ laptop (GPIO_RX)
    assign LEDR[17]   = (led_com_timer > 0);      // có dữ liệu từ ESP32 (cổng COM)
endmodule