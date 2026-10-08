namespace MAX30102_GUI
{
    partial class form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.lblTitle = new System.Windows.Forms.Label();
            this.grpConnection = new System.Windows.Forms.GroupBox();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.cboBaudRate = new System.Windows.Forms.ComboBox();
            this.cboPort = new System.Windows.Forms.ComboBox();
            this.lblBaudRate = new System.Windows.Forms.Label();
            this.lblPort = new System.Windows.Forms.Label();
            this.grpPPG = new System.Windows.Forms.GroupBox();
            this.chartPPG = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.grpMetrics = new System.Windows.Forms.GroupBox();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.txtSpO2 = new System.Windows.Forms.TextBox();
            this.txtBPM = new System.Windows.Forms.TextBox();
            this.lblSpO2Title = new System.Windows.Forms.Label();
            this.lblBPMTitle = new System.Windows.Forms.Label();
            this.grpTest = new System.Windows.Forms.GroupBox();
            this.lblTestProgress = new System.Windows.Forms.Label();
            this.progressBarTest = new System.Windows.Forms.ProgressBar();
            this.btnTest = new System.Windows.Forms.Button();
            this.txtTestResult = new System.Windows.Forms.TextBox();
            this.grpSendReceive = new System.Windows.Forms.GroupBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtReceive = new System.Windows.Forms.TextBox();
            this.txtSend = new System.Windows.Forms.TextBox();
            this.lblReceive = new System.Windows.Forms.Label();
            this.lblSend = new System.Windows.Forms.Label();
            this.serialPort1 = new System.IO.Ports.SerialPort(this.components);
            this.grpConnection.SuspendLayout();
            this.grpPPG.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chartPPG)).BeginInit();
            this.grpMetrics.SuspendLayout();
            this.grpTest.SuspendLayout();
            this.grpSendReceive.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.ForeColor = System.Drawing.Color.Red;
            this.lblTitle.Location = new System.Drawing.Point(133, -2);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(467, 65);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "MAX30102 HEALTH MONITOR";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // grpConnection
            // 
            this.grpConnection.Controls.Add(this.txtStatus);
            this.grpConnection.Controls.Add(this.lblStatus);
            this.grpConnection.Controls.Add(this.btnDisconnect);
            this.grpConnection.Controls.Add(this.btnConnect);
            this.grpConnection.Controls.Add(this.cboBaudRate);
            this.grpConnection.Controls.Add(this.cboPort);
            this.grpConnection.Controls.Add(this.lblBaudRate);
            this.grpConnection.Controls.Add(this.lblPort);
            this.grpConnection.Location = new System.Drawing.Point(13, 65);
            this.grpConnection.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpConnection.Name = "grpConnection";
            this.grpConnection.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpConnection.Size = new System.Drawing.Size(269, 203);
            this.grpConnection.TabIndex = 1;
            this.grpConnection.TabStop = false;
            this.grpConnection.Text = "CONNECTION";
            // 
            // txtStatus
            // 
            this.txtStatus.BackColor = System.Drawing.Color.Red;
            this.txtStatus.Location = new System.Drawing.Point(115, 114);
            this.txtStatus.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.Size = new System.Drawing.Size(104, 22);
            this.txtStatus.TabIndex = 7;
            this.txtStatus.TabStop = false;
            this.txtStatus.Text = "Disconnected";
            this.txtStatus.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(21, 114);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(47, 16);
            this.lblStatus.TabIndex = 6;
            this.lblStatus.Text = "Status:";
            // 
            // btnDisconnect
            // 
            this.btnDisconnect.Location = new System.Drawing.Point(136, 149);
            this.btnDisconnect.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(117, 37);
            this.btnDisconnect.TabIndex = 5;
            this.btnDisconnect.Text = "DISCONNECT";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(16, 151);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(99, 34);
            this.btnConnect.TabIndex = 4;
            this.btnConnect.Text = "CONNECT";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // cboBaudRate
            // 
            this.cboBaudRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboBaudRate.FormattingEnabled = true;
            this.cboBaudRate.Items.AddRange(new object[] {
            "9600"});
            this.cboBaudRate.Location = new System.Drawing.Point(125, 71);
            this.cboBaudRate.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cboBaudRate.Name = "cboBaudRate";
            this.cboBaudRate.Size = new System.Drawing.Size(93, 24);
            this.cboBaudRate.TabIndex = 3;
            // 
            // cboPort
            // 
            this.cboPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboPort.FormattingEnabled = true;
            this.cboPort.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6"});
            this.cboPort.Location = new System.Drawing.Point(125, 31);
            this.cboPort.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cboPort.Name = "cboPort";
            this.cboPort.Size = new System.Drawing.Size(92, 24);
            this.cboPort.TabIndex = 2;
            // 
            // lblBaudRate
            // 
            this.lblBaudRate.AutoSize = true;
            this.lblBaudRate.Location = new System.Drawing.Point(21, 71);
            this.lblBaudRate.Name = "lblBaudRate";
            this.lblBaudRate.Size = new System.Drawing.Size(62, 16);
            this.lblBaudRate.TabIndex = 1;
            this.lblBaudRate.Text = "Baudrate";
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(21, 39);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(64, 16);
            this.lblPort.TabIndex = 0;
            this.lblPort.Text = "COM Port";
            // 
            // grpPPG
            // 
            this.grpPPG.Controls.Add(this.chartPPG);
            this.grpPPG.Location = new System.Drawing.Point(727, 65);
            this.grpPPG.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpPPG.Name = "grpPPG";
            this.grpPPG.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpPPG.Size = new System.Drawing.Size(415, 345);
            this.grpPPG.TabIndex = 2;
            this.grpPPG.TabStop = false;
            this.grpPPG.Text = "PPG WAVEFORM";
            // 
            // chartPPG
            // 
            chartArea2.Name = "ChartArea1";
            this.chartPPG.ChartAreas.Add(chartArea2);
            legend2.Name = "Legend1";
            this.chartPPG.Legends.Add(legend2);
            this.chartPPG.Location = new System.Drawing.Point(33, 55);
            this.chartPPG.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.chartPPG.Name = "chartPPG";
            series2.ChartArea = "ChartArea1";
            series2.Legend = "Legend1";
            series2.Name = "Series1";
            this.chartPPG.Series.Add(series2);
            this.chartPPG.Size = new System.Drawing.Size(358, 266);
            this.chartPPG.TabIndex = 7;
            this.chartPPG.Text = "PPG waveform";
            // 
            // grpMetrics
            // 
            this.grpMetrics.Controls.Add(this.btnExport);
            this.grpMetrics.Controls.Add(this.btnStop);
            this.grpMetrics.Controls.Add(this.btnStart);
            this.grpMetrics.Controls.Add(this.txtSpO2);
            this.grpMetrics.Controls.Add(this.txtBPM);
            this.grpMetrics.Controls.Add(this.lblSpO2Title);
            this.grpMetrics.Controls.Add(this.lblBPMTitle);
            this.grpMetrics.Location = new System.Drawing.Point(13, 274);
            this.grpMetrics.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpMetrics.Name = "grpMetrics";
            this.grpMetrics.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpMetrics.Size = new System.Drawing.Size(269, 154);
            this.grpMetrics.TabIndex = 3;
            this.grpMetrics.TabStop = false;
            this.grpMetrics.Text = "MEASUREMENTS";
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(177, 108);
            this.btnExport.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 28);
            this.btnExport.TabIndex = 6;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(97, 108);
            this.btnStop.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 28);
            this.btnStop.TabIndex = 5;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(16, 108);
            this.btnStart.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 28);
            this.btnStart.TabIndex = 4;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // txtSpO2
            // 
            this.txtSpO2.BackColor = System.Drawing.Color.White;
            this.txtSpO2.Location = new System.Drawing.Point(115, 68);
            this.txtSpO2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtSpO2.Name = "txtSpO2";
            this.txtSpO2.ReadOnly = true;
            this.txtSpO2.Size = new System.Drawing.Size(57, 22);
            this.txtSpO2.TabIndex = 3;
            this.txtSpO2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtBPM
            // 
            this.txtBPM.BackColor = System.Drawing.Color.White;
            this.txtBPM.Location = new System.Drawing.Point(115, 37);
            this.txtBPM.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtBPM.Name = "txtBPM";
            this.txtBPM.ReadOnly = true;
            this.txtBPM.Size = new System.Drawing.Size(57, 22);
            this.txtBPM.TabIndex = 2;
            this.txtBPM.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblSpO2Title
            // 
            this.lblSpO2Title.AutoSize = true;
            this.lblSpO2Title.Location = new System.Drawing.Point(20, 74);
            this.lblSpO2Title.Name = "lblSpO2Title";
            this.lblSpO2Title.Size = new System.Drawing.Size(38, 16);
            this.lblSpO2Title.TabIndex = 1;
            this.lblSpO2Title.Text = "SpO₂";
            // 
            // lblBPMTitle
            // 
            this.lblBPMTitle.AutoSize = true;
            this.lblBPMTitle.Location = new System.Drawing.Point(21, 43);
            this.lblBPMTitle.Name = "lblBPMTitle";
            this.lblBPMTitle.Size = new System.Drawing.Size(36, 16);
            this.lblBPMTitle.TabIndex = 0;
            this.lblBPMTitle.Text = "BPM";
            // 
            // grpTest
            // 
            this.grpTest.Controls.Add(this.lblTestProgress);
            this.grpTest.Controls.Add(this.progressBarTest);
            this.grpTest.Controls.Add(this.btnTest);
            this.grpTest.Controls.Add(this.txtTestResult);
            this.grpTest.Location = new System.Drawing.Point(347, 65);
            this.grpTest.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpTest.Name = "grpTest";
            this.grpTest.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpTest.Size = new System.Drawing.Size(347, 124);
            this.grpTest.TabIndex = 4;
            this.grpTest.TabStop = false;
            this.grpTest.Text = "TEST";
            // 
            // lblTestProgress
            // 
            this.lblTestProgress.AutoSize = true;
            this.lblTestProgress.Location = new System.Drawing.Point(163, 39);
            this.lblTestProgress.Name = "lblTestProgress";
            this.lblTestProgress.Size = new System.Drawing.Size(26, 16);
            this.lblTestProgress.TabIndex = 3;
            this.lblTestProgress.Text = "0%";
            // 
            // progressBarTest
            // 
            this.progressBarTest.Location = new System.Drawing.Point(33, 31);
            this.progressBarTest.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.progressBarTest.Name = "progressBarTest";
            this.progressBarTest.Size = new System.Drawing.Size(289, 36);
            this.progressBarTest.TabIndex = 2;
            // 
            // btnTest
            // 
            this.btnTest.Location = new System.Drawing.Point(59, 80);
            this.btnTest.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(59, 25);
            this.btnTest.TabIndex = 1;
            this.btnTest.Text = "TEST";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // txtTestResult
            // 
            this.txtTestResult.BackColor = System.Drawing.Color.White;
            this.txtTestResult.Location = new System.Drawing.Point(179, 82);
            this.txtTestResult.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtTestResult.Name = "txtTestResult";
            this.txtTestResult.ReadOnly = true;
            this.txtTestResult.Size = new System.Drawing.Size(127, 22);
            this.txtTestResult.TabIndex = 0;
            this.txtTestResult.Text = "0/1000";
            this.txtTestResult.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // grpSendReceive
            // 
            this.grpSendReceive.Controls.Add(this.btnSend);
            this.grpSendReceive.Controls.Add(this.txtReceive);
            this.grpSendReceive.Controls.Add(this.txtSend);
            this.grpSendReceive.Controls.Add(this.lblReceive);
            this.grpSendReceive.Controls.Add(this.lblSend);
            this.grpSendReceive.Location = new System.Drawing.Point(347, 241);
            this.grpSendReceive.Margin = new System.Windows.Forms.Padding(4);
            this.grpSendReceive.Name = "grpSendReceive";
            this.grpSendReceive.Padding = new System.Windows.Forms.Padding(4);
            this.grpSendReceive.Size = new System.Drawing.Size(347, 194);
            this.grpSendReceive.TabIndex = 5;
            this.grpSendReceive.TabStop = false;
            this.grpSendReceive.Text = "Data Send/Receive";
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(39, 159);
            this.btnSend.Margin = new System.Windows.Forms.Padding(4);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(100, 28);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "SEND";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // txtReceive
            // 
            this.txtReceive.BackColor = System.Drawing.Color.White;
            this.txtReceive.Location = new System.Drawing.Point(159, 110);
            this.txtReceive.Margin = new System.Windows.Forms.Padding(4);
            this.txtReceive.Name = "txtReceive";
            this.txtReceive.ReadOnly = true;
            this.txtReceive.Size = new System.Drawing.Size(132, 22);
            this.txtReceive.TabIndex = 3;
            this.txtReceive.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtSend
            // 
            this.txtSend.Location = new System.Drawing.Point(161, 49);
            this.txtSend.Margin = new System.Windows.Forms.Padding(4);
            this.txtSend.Name = "txtSend";
            this.txtSend.Size = new System.Drawing.Size(132, 22);
            this.txtSend.TabIndex = 2;
            // 
            // lblReceive
            // 
            this.lblReceive.AutoSize = true;
            this.lblReceive.Location = new System.Drawing.Point(43, 105);
            this.lblReceive.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblReceive.Name = "lblReceive";
            this.lblReceive.Size = new System.Drawing.Size(61, 16);
            this.lblReceive.TabIndex = 1;
            this.lblReceive.Text = "Receive:";
            // 
            // lblSend
            // 
            this.lblSend.AutoSize = true;
            this.lblSend.Location = new System.Drawing.Point(43, 53);
            this.lblSend.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSend.Name = "lblSend";
            this.lblSend.Size = new System.Drawing.Size(42, 16);
            this.lblSend.TabIndex = 0;
            this.lblSend.Text = "Send:";
            // 
            // serialPort1
            // 
            this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.SerialPort_DataReceived);
            // 
            // form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1168, 450);
            this.Controls.Add(this.grpSendReceive);
            this.Controls.Add(this.grpTest);
            this.Controls.Add(this.grpMetrics);
            this.Controls.Add(this.grpPPG);
            this.Controls.Add(this.grpConnection);
            this.Controls.Add(this.lblTitle);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "form1";
            this.Text = "MAX30102 HEALTH MONITOR";
            this.grpConnection.ResumeLayout(false);
            this.grpConnection.PerformLayout();
            this.grpPPG.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.chartPPG)).EndInit();
            this.grpMetrics.ResumeLayout(false);
            this.grpMetrics.PerformLayout();
            this.grpTest.ResumeLayout(false);
            this.grpTest.PerformLayout();
            this.grpSendReceive.ResumeLayout(false);
            this.grpSendReceive.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox grpConnection;
        private System.Windows.Forms.GroupBox grpPPG;
        private System.Windows.Forms.Label lblBaudRate;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.GroupBox grpMetrics;
        private System.Windows.Forms.GroupBox grpTest;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.ComboBox cboBaudRate;
        private System.Windows.Forms.ComboBox cboPort;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartPPG;
        private System.Windows.Forms.Label lblSpO2Title;
        private System.Windows.Forms.Label lblBPMTitle;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.TextBox txtBPM;
        private System.Windows.Forms.TextBox txtSpO2;
        private System.Windows.Forms.ProgressBar progressBarTest;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.TextBox txtTestResult;
        private System.Windows.Forms.Label lblTestProgress;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.GroupBox grpSendReceive;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.TextBox txtReceive;
        private System.Windows.Forms.TextBox txtSend;
        private System.Windows.Forms.Label lblReceive;
        private System.Windows.Forms.Label lblSend;
        private System.IO.Ports.SerialPort serialPort1;
    }
}

