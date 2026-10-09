module lcd_writer(
    input        clk,
    input        rst_n,
    input  [7:0] hr_value,
    input  [7:0] spo2_value,
    output       LCD_ON, LCD_BLON, LCD_RW, LCD_EN, LCD_RS,
    output [7:0] LCD_DATA
);
    assign LCD_ON = 1'b1; assign LCD_BLON = 1'b1; assign LCD_RW = 1'b0;

    wire [7:0] hr_h = "0" + (hr_value/100);
    wire [7:0] hr_t = "0" + ((hr_value/10)%10);
    wire [7:0] hr_o = "0" + (hr_value%10);
    wire [7:0] sp_h = "0" + (spo2_value/100);
    wire [7:0] sp_t = "0" + ((spo2_value/10)%10);
    wire [7:0] sp_o = "0" + (spo2_value%10);

    function [8:0] rom_lookup;
        input [5:0] addr;
        begin
            case (addr)
                0:  rom_lookup = {1'b0, 8'h38}; // function set 8bit,2line
                1:  rom_lookup = {1'b0, 8'h0C}; // display on
                2:  rom_lookup = {1'b0, 8'h01}; // clear
                3:  rom_lookup = {1'b0, 8'h06}; // entry mode
                4:  rom_lookup = {1'b0, 8'h80}; // dong 1, cot 0
                5:  rom_lookup = {1'b1, "H"};
                6:  rom_lookup = {1'b1, "e"};
                7:  rom_lookup = {1'b1, "a"};
                8:  rom_lookup = {1'b1, "r"};
                9:  rom_lookup = {1'b1, "t"};
                10: rom_lookup = {1'b1, ":"};
                11: rom_lookup = {1'b1, " "};
                15: rom_lookup = {1'b1, "b"};
                16: rom_lookup = {1'b1, "p"};
                17: rom_lookup = {1'b1, "m"};
                18: rom_lookup = {1'b0, 8'hC0}; // dong 2, cot 0
                19: rom_lookup = {1'b1, "S"};
                20: rom_lookup = {1'b1, "p"};
                21: rom_lookup = {1'b1, "O"};
                22: rom_lookup = {1'b1, "2"};
                23: rom_lookup = {1'b1, ":"};
                24: rom_lookup = {1'b1, " "};
                28: rom_lookup = {1'b1, "%"};
                default: rom_lookup = {1'b0, 8'h00};
            endcase
        end
    endfunction

    localparam LAST_STEP = 28;
    reg [5:0]  step;
    reg [19:0] delay_cnt;
    reg [1:0]  phase;
    reg        en_pulse;
    reg        cur_rs;
    reg [7:0]  cur_data;
    reg [8:0]  rom_val;

    always @(*) begin
        case (step)
            12: rom_val = {1'b1, hr_h};
            13: rom_val = {1'b1, hr_t};
            14: rom_val = {1'b1, hr_o};
            25: rom_val = {1'b1, sp_h};
            26: rom_val = {1'b1, sp_t};
            27: rom_val = {1'b1, sp_o};
            default: rom_val = rom_lookup(step);
        endcase
    end

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            step <= 0; phase <= 2; delay_cnt <= 20'd750_000; en_pulse <= 0;
        end else if (delay_cnt != 0) begin
            delay_cnt <= delay_cnt - 1;
        end else begin
            case (phase)
                2: begin
                    cur_rs   <= rom_val[8];
                    cur_data <= rom_val[7:0];
                    delay_cnt <= 20'd5;
                    phase <= 0;
                end
                0: begin
                    en_pulse <= 1;
                    delay_cnt <= 20'd10;
                    phase <= 1;
                end
                1: begin
                    en_pulse <= 0;
                    delay_cnt <= (step==2 || step==4 || step==18) ? 20'd100_000 : 20'd2_500;
                    step <= (step == LAST_STEP) ? 6'd4 : step + 1; // lap lai, bo qua khoi tao
                    phase <= 2;
                end
            endcase
        end
    end

    assign LCD_EN = en_pulse;
    assign LCD_RS = cur_rs;
    assign LCD_DATA = cur_data;
endmodule