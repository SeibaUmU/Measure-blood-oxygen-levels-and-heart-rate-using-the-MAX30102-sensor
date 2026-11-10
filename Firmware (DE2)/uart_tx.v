module uart_tx #(
    parameter CLK_FREQ    = 50_000_000,
    parameter BAUD_RATE   = 9600,
    parameter DATA_BITS   = 8, // 7 hoặc 8 bit
    parameter PARITY_MODE = 0, // 0: None, 1: Even, 2: Odd
    parameter STOP_BITS   = 1  // 1 hoặc 2 stop bit
)(
    input        clk,
    input        rst_n,
    input        tx_req,
    input  [7:0] tx_data,
    output reg   tx_pin,
    output       tx_busy 
);
    localparam BIT_PERIOD = CLK_FREQ / BAUD_RATE;
    localparam IDLE=3'd0, START=3'd1, DATA=3'd2, PARITY=3'd3, STOP=3'd4;

    reg [2:0]  state;
    reg [15:0] clk_cnt;
    reg [2:0]  bit_idx;
    reg [7:0]  shift_reg;

    assign tx_busy = (state != IDLE) || tx_req; // Báo bận khi đang truyền hoặc có yêu cầu truyền mới

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state   <= IDLE;
            tx_pin  <= 1'b1;
            clk_cnt <= 0;
            bit_idx <= 0;
            shift_reg <= 0;
        end else begin
            case (state)
                IDLE: begin
                    tx_pin <= 1'b1; 
                    if (tx_req) begin
                        shift_reg <= tx_data; // Lưu dữ liệu cần truyền vào thanh ghi dịch
                        state     <= START;
                        clk_cnt   <= 0;
                    end
                end
                START: begin
                    tx_pin <= 1'b0; //Sự sụt áp này chính là dấu hiệu (Start bit) đánh thức thiết bị nhận 
                    //Khi đếm đến BIT_PERIOD - 1 (đủ 5207 chu kỳ đồng hồ - tương đương thời lượng 104us), quá trình phát Start bit kết thúc. 
                    //Mạch reset giờ, chuẩn bị bắn dữ liệu và chuyển qua trạng thái DATA
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        state   <= DATA;
                        bit_idx <= 0;
                    end else clk_cnt <= clk_cnt + 1;
                end
                DATA: begin
                    tx_pin <= shift_reg[bit_idx]; // Bắn từng bit dữ liệu từ thanh ghi dịch ra chân tx_pin, bat dau tu bit 0 (LSB)
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        if (bit_idx == DATA_BITS - 1)
                            state <= (PARITY_MODE != 0) ? PARITY : STOP;
                        else
                            bit_idx <= bit_idx + 1;
                    end else clk_cnt <= clk_cnt + 1; 
                end
                PARITY: begin
                    tx_pin <= (PARITY_MODE == 1) ? (^shift_reg) : ~(^shift_reg);
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        state   <= STOP;
                    end else clk_cnt <= clk_cnt + 1;
                end
                STOP: begin
                    tx_pin <= 1'b1;
                    if (clk_cnt == (BIT_PERIOD * STOP_BITS) - 1) begin
                        clk_cnt <= 0;
                        state   <= IDLE;
                    end else clk_cnt <= clk_cnt + 1;
                end
                default: state <= IDLE;
            endcase
        end
    end
endmodule