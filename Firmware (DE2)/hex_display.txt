module hex_display(
    input  [7:0] hr_value,
    input  [7:0] spo2_value,
    output [6:0] HEX7, HEX6, HEX5, // Nhip tim: tram, chuc, don vi
    output [6:0] HEX4,             // tat, ngan cach 2 nhom so
    output [6:0] HEX3, HEX2, HEX1, // SpO2: tram, chuc, don vi
    output [6:0] HEX0              // tat
);
    // Bang giai ma BCD -> 7 doan, active-low (0 = sang, 1 = tat)
    function [6:0] seg7;
        input [3:0] d;
        begin
            case (d)
                4'h0: seg7 = 7'b1000000;
                4'h1: seg7 = 7'b1111001;
                4'h2: seg7 = 7'b0100100;
                4'h3: seg7 = 7'b0110000;
                4'h4: seg7 = 7'b0011001;
                4'h5: seg7 = 7'b0010010;
                4'h6: seg7 = 7'b0000010;
                4'h7: seg7 = 7'b1111000;
                4'h8: seg7 = 7'b0000000;
                4'h9: seg7 = 7'b0010000;
                default: seg7 = 7'b1111111;
            endcase
        end
    endfunction

    assign HEX7 = seg7(hr_value / 100);
    assign HEX6 = seg7((hr_value / 10) % 10);
    assign HEX5 = seg7(hr_value % 10);
    assign HEX4 = 7'b1111111; // tat

    assign HEX3 = seg7(spo2_value / 100);
    assign HEX2 = seg7((spo2_value / 10) % 10);
    assign HEX1 = seg7(spo2_value % 10);
    assign HEX0 = 7'b1111111; // tat
endmodule