module uart_rx #(
    parameter CLK_FREQ    = 50_000_000,
    parameter BAUD_RATE   = 9600,
    parameter DATA_BITS   = 8, // Hỗ trợ cấu hình 7 hoặc 8 bit (FW-07) 8N1
    parameter PARITY_MODE = 0  // 0: None, 1: Even (Chẵn), 2: Odd (Lẻ) (FW-07, FW-09)
)(
    input            clk,
    input            rst_n,
    input            rx,
    output reg [7:0] data_out, 
    output reg       data_valid,
    output reg       framing_error, // Báo lỗi Stop bit != 1 (FW-08, FW-09)
    output reg       parity_error   // Báo lỗi bit chẵn lẻ (FW-09)
);
    localparam BIT_PERIOD = CLK_FREQ / BAUD_RATE; // Số chu kỳ clock cho 1 bit UART, Với 50MHz và 9600bps, BIT_PERIOD = 5208 chu kỳ
    localparam IDLE=3'd0, START=3'd1, DATA=3'd2, PARITY=3'd3, STOP=3'd4; // Trạng thái FSM: IDLE, START, DATA, PARITY, STOP

    reg rx_sync1, rx_sync2; // Đồng bộ tín hiệu rx với clock hệ thống để tránh metastability (FW-08)
    /*
    Mạch chống Meta-stability (Bất ổn định mức logic): 
    Đây là kỹ thuật dùng 2 thanh ghi dịch (Flip-Flop) nối tiếp nhau. 
    Do tín hiệu rx đi từ bên ngoài vào không đồng bộ với xung nhịp 50MHz của FPGA, 
    nó có thể rơi vào vùng điện áp lửng lơ. rx_sync1 và rx_sync2 sẽ lọc tín hiệu này, 
    đưa nó về các mức 0 và 1 vuông vắn để FSM đọc chuẩn xác nhất.
    */
    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            rx_sync1 <= 1'b1;
            rx_sync2 <= 1'b1;
        end else begin
            rx_sync1 <= rx; 
            rx_sync2 <= rx_sync1; 
        end
    end

    reg [2:0]  state;
    reg [15:0] clk_cnt; // Bộ đếm clock để xác định thời gian mỗi bit UART
    reg [2:0]  bit_idx; // Chỉ số bit dữ liệu đang nhận (0 đến 7 cho 8 bit dữ liệu)
    reg [7:0]  shift_reg; // Thanh ghi dịch để lưu trữ dữ liệu nhận được từ UART,làm nhiệm vụ gom từng bit đơn lẻ nối tiếp thành 1 byte song song.
    reg        rx_parity_bit; // Lưu trữ bit chẵn lẻ nhận được từ UART để kiểm tra lỗi (FW-09)
    //Phép toán XOR (^shift_reg) tự động tính trước bit chẵn/lẻ dự kiến của byte vừa nhận để so sánh với bit parity thực tế (rx_parity_bit) nhằm tìm ra lỗi truyền tải.
    wire calc_parity = (PARITY_MODE == 1) ? (^shift_reg) : ~(^shift_reg); // Tính toán bit chẵn lẻ dựa trên dữ liệu nhận được (FW-09)

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= IDLE; clk_cnt <= 0; bit_idx <= 0;
            data_out <= 0; data_valid <= 0;
            framing_error <= 0; parity_error <= 0;
            shift_reg <= 0; rx_parity_bit <= 0;
        end else begin
            data_valid <= 0; // Cờ này luôn tự động tụt về 0 ở mỗi nhịp đồng hồ, chỉ nảy 1 khi xong gói
            case (state)
                IDLE: begin
                    clk_cnt <= 0;
                    if (rx_sync2 == 1'b0) state <= START; // Phát hiện cạnh xuống Start bit (FW-08)
                end
                START: begin
                    if (clk_cnt == BIT_PERIOD/2) begin    // Kiểm tra lại giữa chu ky Start bit (FW-08)
                        clk_cnt <= 0;
                        state   <= (rx_sync2 == 1'b0) ? DATA : IDLE;
                        bit_idx <= 0;
                    end else clk_cnt <= clk_cnt + 1;
                end
                DATA: begin
                    if (clk_cnt == BIT_PERIOD - 1) begin  // Lấy mẫu chính giữa mỗi bit dữ liệu (FW-08)
                        clk_cnt <= 0; // Reset bộ đếm clock để chuẩn bị cho bit tiếp theo
                        shift_reg[bit_idx] <= rx_sync2; // Lưu bit dữ liệu nhận được vào thanh ghi dịch
                        if (bit_idx == DATA_BITS - 1)
                            state <= (PARITY_MODE != 0) ? PARITY : STOP;
                        else
                            bit_idx <= bit_idx + 1;
                    end else clk_cnt <= clk_cnt + 1; 
                end
                PARITY: begin //hien tai ko dung
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        rx_parity_bit <= rx_sync2;
                        state <= STOP;
                    end else clk_cnt <= clk_cnt + 1;
                end
                STOP: begin
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        // Kiểm tra Stop bit phải ở mức cao 1'b1 (FW-08, FW-09)
                        if (rx_sync2 == 1'b1) begin
                            framing_error <= 1'b0;
                            if (PARITY_MODE != 0 && (rx_parity_bit != calc_parity)) begin
                                parity_error <= 1'b1;
                            end else begin
                                parity_error <= 1'b0;
                                data_out     <= shift_reg;
                                data_valid   <= 1'b1;
                            end
                        end else begin
                            framing_error <= 1'b1; // Báo lỗi Framing Error khi Stop bit = 0
                        end
                        state <= IDLE;
                    end else clk_cnt <= clk_cnt + 1; 
                end
                default: state <= IDLE;
            endcase
        end
    end
endmodule