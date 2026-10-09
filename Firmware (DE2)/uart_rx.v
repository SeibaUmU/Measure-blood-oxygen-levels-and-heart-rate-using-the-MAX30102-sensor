module uart_rx #(
    parameter CLK_FREQ  = 50_000_000,
    parameter BAUD_RATE = 9600
)(
    input        clk,
    input        rst_n,
    input        rx,
    output reg [7:0] data_out,
    output reg       data_valid
);
    localparam BIT_PERIOD = CLK_FREQ / BAUD_RATE;
    localparam IDLE=0, START=1, DATA=2, STOP=3;

    reg rx_sync1, rx_sync2;
    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            rx_sync1 <= 1'b1;
            rx_sync2 <= 1'b1;
        end else begin
            rx_sync1 <= rx;
            rx_sync2 <= rx_sync1;
        end
    end

    reg [1:0]  state;
    reg [15:0] clk_cnt;
    reg [2:0]  bit_idx;
    reg [7:0]  shift_reg;

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= IDLE; clk_cnt <= 0; bit_idx <= 0; data_valid <= 0;
        end else begin
            data_valid <= 0;
            case (state)
                IDLE: begin
                    clk_cnt <= 0;
                    if (rx_sync2 == 0) state <= START;
                end
                START: begin
                    if (clk_cnt == BIT_PERIOD/2) begin
                        clk_cnt <= 0;
                        state <= (rx_sync2 == 0) ? DATA : IDLE; 
                        bit_idx <= 0;
                    end else clk_cnt <= clk_cnt + 1;
                end
                DATA: begin
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        shift_reg[bit_idx] <= rx_sync2; 
                        if (bit_idx == 7) state <= STOP;
                        else bit_idx <= bit_idx + 1;
                    end else clk_cnt <= clk_cnt + 1;
                end
                STOP: begin
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        data_out <= shift_reg;
                        data_valid <= 1;
                        state <= IDLE;
                    end else clk_cnt <= clk_cnt + 1;
                end
                default: state <= IDLE;
            endcase
        end
    end
endmodule