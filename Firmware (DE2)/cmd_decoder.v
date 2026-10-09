// Giải mã kịch bản B: [@][0x02][CMD][&][CRC]  (GUI -> DE2)
module cmd_decoder(
    input        clk,
    input        rst_n,
    input  [7:0] byte_in,
    input        byte_valid,
    output reg [7:0] cmd_code,   // '1'=0x31 Start, '0'=0x30 Stop, 'R'=0x52 Reset
    output reg       cmd_valid   // xung 1 chu kỳ khi nhận đúng 1 frame lệnh
);
    localparam WAIT_HEADER=0, WAIT_TYPE=1, WAIT_DATA=2, WAIT_FOOTER=3, WAIT_CRC=4;
    reg [2:0] state;

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= WAIT_HEADER; cmd_valid <= 0; cmd_code <= 0;
        end else begin
            cmd_valid <= 0;
            if (byte_valid) begin
                case (state)
                    WAIT_HEADER: state <= (byte_in == 8'h40) ? WAIT_TYPE : WAIT_HEADER;
                    WAIT_TYPE:   state <= (byte_in == 8'h02) ? WAIT_DATA : WAIT_HEADER;
                    WAIT_DATA:   begin cmd_code <= byte_in; state <= WAIT_FOOTER; end
                    WAIT_FOOTER: state <= (byte_in == 8'h26) ? WAIT_CRC : WAIT_HEADER;
                    WAIT_CRC: begin
                        if (byte_in == (8'h02 ^ cmd_code ^ 8'h26)) cmd_valid <= 1;
                        state <= WAIT_HEADER;
                    end
                    default: state <= WAIT_HEADER;
                endcase
            end
        end
    end
endmodule