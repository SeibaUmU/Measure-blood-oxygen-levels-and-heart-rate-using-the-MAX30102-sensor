module frame_decoder(
    input        clk,
    input        rst_n,
    input        clear,          // xóa HR/SpO2 (khi GUI gửi Start hoặc Reset)
    input  [7:0] byte_in,
    input        byte_valid,
    // Kịch bản A (type 0x01): kết quả đo
    output reg [7:0]  hr_value,
    output reg [7:0]  spo2_value,
    output reg        new_data,
    // Kịch bản C (type 0x03): dữ liệu thô PPG 16-bit
    output reg [15:0] ppg_value,
    output reg        new_ppg
);
    localparam WAIT_HEADER=0, WAIT_TYPE=1, WAIT_DATA1=2, WAIT_DATA2=3, WAIT_FOOTER=4, WAIT_CRC=5;
    reg [2:0] state;
    reg [7:0] type_reg, data1_reg, data2_reg;

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= WAIT_HEADER;
            new_data <= 0; new_ppg <= 0;
            hr_value <= 0; spo2_value <= 0; ppg_value <= 0;
        end else begin
            new_data <= 0;
            new_ppg  <= 0;
            if (clear) begin
                hr_value <= 0; spo2_value <= 0;
                state <= WAIT_HEADER;
            end else if (byte_valid) begin
                case (state)
                    WAIT_HEADER: state <= (byte_in == 8'h40) ? WAIT_TYPE : WAIT_HEADER;
                    WAIT_TYPE:   begin
                        type_reg <= byte_in;
                        // 0x01 = kết quả (A), 0x03 = raw PPG (C): cùng cấu trúc 2 byte data
                        state <= (byte_in == 8'h01 || byte_in == 8'h03) ? WAIT_DATA1 : WAIT_HEADER;
                    end
                    WAIT_DATA1:  begin data1_reg <= byte_in; state <= WAIT_DATA2; end
                    WAIT_DATA2:  begin data2_reg <= byte_in; state <= WAIT_FOOTER; end
                    WAIT_FOOTER: state <= (byte_in == 8'h26) ? WAIT_CRC : WAIT_HEADER;
                    WAIT_CRC: begin
                        if (byte_in == (type_reg ^ data1_reg ^ data2_reg ^ 8'h26)) begin
                            if (type_reg == 8'h01) begin
                                hr_value   <= data1_reg;
                                spo2_value <= data2_reg;
                                new_data   <= 1;
                            end else begin
                                ppg_value  <= {data1_reg, data2_reg};
                                new_ppg    <= 1;
                            end
                        end
                        state <= WAIT_HEADER;
                    end
                    default: state <= WAIT_HEADER;
                endcase
            end
        end
    end
endmodule