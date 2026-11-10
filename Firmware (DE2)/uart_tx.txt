module uart_tx #(
    parameter CLK_FREQ  = 50_000_000,
    parameter BAUD_RATE = 9600
)(
    input        clk,
    input        rst_n,
    input        tx_req,
    input  [7:0] tx_data,
    output reg   tx_pin,
    output       tx_busy
);
    localparam BIT_PERIOD = CLK_FREQ / BAUD_RATE;
    reg [1:0]  state; // 0: IDLE, 1: START, 2: DATA, 3: STOP
    reg [15:0] clk_cnt;
    reg [2:0]  bit_idx;
    reg [7:0]  shift_reg;

    assign tx_busy = (state != 0) || tx_req;

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state   <= 0;
            tx_pin  <= 1'b1;
            clk_cnt <= 0;
            bit_idx <= 0;
        end else begin
            case (state)
                0: begin // IDLE
                    tx_pin <= 1'b1;
                    if (tx_req) begin
                        shift_reg <= tx_data;
                        state     <= 1;
                        clk_cnt   <= 0;
                    end
                end
                1: begin // START
                    tx_pin <= 1'b0;
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        state   <= 2;
                        bit_idx <= 0;
                    end else clk_cnt <= clk_cnt + 1;
                end
                2: begin // DATA
                    tx_pin <= shift_reg[bit_idx];
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        if (bit_idx == 7) state <= 3;
                        else bit_idx <= bit_idx + 1;
                    end else clk_cnt <= clk_cnt + 1;
                end
                3: begin // STOP
                    tx_pin <= 1'b1;
                    if (clk_cnt == BIT_PERIOD - 1) begin
                        clk_cnt <= 0;
                        state   <= 0;
                    end else clk_cnt <= clk_cnt + 1;
                end
            endcase
        end
    end
endmodule