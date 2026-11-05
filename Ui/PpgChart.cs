using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using MAX30102_WPF.Measurement;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using SPColor = ScottPlot.Color;

namespace MAX30102_WPF.Ui
{
    /// <summary>
    /// Điều khiển biểu đồ sóng PPG (ScottPlot 5): vẽ cửa sổ trượt, đường con trỏ theo chuột,
    /// ghim (pin) các đường để so sánh, và chế độ Freeze.
    /// <para>Trục X là số thứ tự mẫu tuyệt đối, nên đường đã ghim đứng yên trên đúng mẫu
    /// trong khi cửa sổ trượt sang trái. Toàn bộ lớp này chạy trên luồng UI.</para>
    /// </summary>
    public sealed class PpgChart
    {
        public const int WindowSize = 200;
        private const int MaxPins = 4;
        private static readonly string[] PinColors = { "#FFC857", "#F472B6", "#A78BFA", "#60A5FA" };

        private sealed class Pin
        {
            public long Index;
            public double Y;
            public string Name;
            public VerticalLine Line;
        }

        private readonly WpfPlot plotControl;
        private readonly TextBlock hoverText;
        private readonly TextBlock pinText;
        private readonly ToggleButton freezeToggle;
        private readonly DispatcherTimer renderTimer;
        private readonly List<Pin> pins = new List<Pin>();

        private bool dirty;

        // Ảnh chụp đang hiển thị trên biểu đồ (đứng yên khi Freeze)
        private double[] snapY = new double[0];
        private long snapFirst;

        private Scatter ppgLine;
        private VerticalLine hoverLine;
        private Marker hoverDot;
        private double mouseX;
        private bool mouseInside;
        private int pinCounter;

        public PpgChart(WpfPlot plotControl, TextBlock hoverText, TextBlock pinText, ToggleButton freezeToggle)
        {
            this.plotControl = plotControl;
            this.hoverText = hoverText;
            this.pinText = pinText;
            this.freezeToggle = freezeToggle;

            Samples = new PpgBuffer(WindowSize);
            SetupChart();

            renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)   // ~30 fps
            };
            renderTimer.Tick += OnRenderTick;
            renderTimer.Start();
        }

        /// <summary>Dữ liệu mẫu đang giữ (dùng cho xuất file).</summary>
        public PpgBuffer Samples { get; private set; }

        // =====================================================================
        //  API CÔNG KHAI
        // =====================================================================

        /// <summary>Thêm một mẫu mới; biểu đồ vẽ lại ở nhịp render kế tiếp.</summary>
        public void AddSample(double value)
        {
            Samples.Add(value);
            dirty = true;
        }

        /// <summary>Xoá dữ liệu, bỏ Freeze và xoá các đường đã ghim.</summary>
        public void Clear()
        {
            Samples.Clear();
            freezeToggle.IsChecked = false;
            RemoveAllPins();
            dirty = true;
        }

        /// <summary>Gọi khi nút Freeze đổi trạng thái: bỏ Freeze thì vẽ lại ngay với dữ liệu mới nhất.</summary>
        public void OnFreezeChanged()
        {
            if (freezeToggle.IsChecked != true) dirty = true;
        }

        /// <summary>Xoá các đường đã ghim và vẽ lại.</summary>
        public void ClearPins()
        {
            RemoveAllPins();
            plotControl.Refresh();
        }

        // =====================================================================
        //  KHỞI TẠO
        // =====================================================================
        private void SetupChart()
        {
            var plt = plotControl.Plot;

            plt.FigureBackground.Color = SPColor.FromHex("#161E2E");
            plt.DataBackground.Color = SPColor.FromHex("#0B111B");
            plt.Axes.Color(SPColor.FromHex("#8593AD"));
            plt.Grid.MajorLineColor = SPColor.FromHex("#1F2B42");
            plt.Axes.SetLimits(0, WindowSize, -1, 1);

            // Chuột do mình tự xử lý (đường con trỏ + ghim), nên tắt pan/zoom có sẵn
            plotControl.UserInputProcessor.IsEnabled = false;

            hoverLine = plt.Add.VerticalLine(0);
            hoverLine.Color = SPColor.FromHex("#A9B6D0");
            hoverLine.LineWidth = 1;
            hoverLine.LinePattern = ScottPlot.LinePattern.Dashed;
            hoverLine.IsVisible = false;

            plotControl.MouseMove += OnMouseMove;
            plotControl.MouseLeave += OnMouseLeave;
            plotControl.PreviewMouseLeftButtonDown += OnMouseClick;

            plotControl.Refresh();
        }

        // =====================================================================
        //  VẼ LẠI THEO NHỊP
        // =====================================================================
        private void OnRenderTick(object sender, EventArgs e)
        {
            if (!dirty) return;
            dirty = false;
            if (freezeToggle.IsChecked == true) return;   // đang Freeze: giữ nguyên hình
            Rebuild();
        }

        private void Rebuild()
        {
            var plt = plotControl.Plot;

            if (ppgLine != null) { plt.Remove(ppgLine); ppgLine = null; }

            snapY = Samples.ToArray();
            snapFirst = Samples.FirstIndex;

            // Bỏ các đường ghim đã trượt ra khỏi cửa sổ
            for (int k = pins.Count - 1; k >= 0; k--)
            {
                if (pins[k].Index < snapFirst)
                {
                    plt.Remove(pins[k].Line);
                    pins.RemoveAt(k);
                }
            }

            if (snapY.Length > 1)
            {
                double[] xs = new double[snapY.Length];
                for (int i = 0; i < xs.Length; i++) xs[i] = snapFirst + i;

                ppgLine = plt.Add.Scatter(xs, snapY);
                ppgLine.Color = SPColor.FromHex("#3DDC97");
                ppgLine.LineWidth = 2;
                ppgLine.MarkerSize = 0;

                double min = snapY.Min();
                double max = snapY.Max();
                double pad = (max - min) * 0.1;
                if (pad < 1) pad = 1;
                plt.Axes.SetLimitsY(min - pad, max + pad);
            }
            plt.Axes.SetLimitsX(snapFirst, snapFirst + WindowSize - 1);

            UpdatePinText();
            UpdateHover();
            plotControl.Refresh();
        }

        // =====================================================================
        //  ĐƯỜNG CON TRỎ THEO CHUỘT
        // =====================================================================
        private double MouseToDataX(System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(plotControl);
            var px = new ScottPlot.Pixel((float)(p.X * plotControl.DisplayScale), (float)(p.Y * plotControl.DisplayScale));
            return plotControl.Plot.GetCoordinates(px).X;
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouseX = MouseToDataX(e);
            mouseInside = true;
            UpdateHover();
            plotControl.Refresh();
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouseInside = false;
            UpdateHover();
            plotControl.Refresh();
        }

        private void UpdateHover()
        {
            var plt = plotControl.Plot;
            if (hoverDot != null) { plt.Remove(hoverDot); hoverDot = null; }

            int i = (int)Math.Round(mouseX - snapFirst);
            if (!mouseInside || snapY.Length == 0 || i < 0 || i >= snapY.Length)
            {
                hoverLine.IsVisible = false;
                hoverText.Text = "Move the mouse over the chart to read values.";
                return;
            }

            double x = snapFirst + i;
            double y = snapY[i];
            hoverLine.X = x;
            hoverLine.IsVisible = true;
            hoverDot = plt.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, 9, SPColor.FromHex("#FFFFFF"));
            hoverText.Text = string.Format(CultureInfo.InvariantCulture, "Sample #{0}    PPG = {1:0.0}", (long)x, y);
        }

        // =====================================================================
        //  GHIM ĐƯỜNG (bấm vào biểu đồ để nhân đôi một đường tại vị trí đó)
        // =====================================================================
        private void OnMouseClick(object sender, MouseButtonEventArgs e)
        {
            int i = (int)Math.Round(MouseToDataX(e) - snapFirst);
            if (snapY.Length == 0 || i < 0 || i >= snapY.Length) return;

            long abs = snapFirst + i;
            if (pins.Any(p => p.Index == abs)) return;

            var line = plotControl.Plot.Add.VerticalLine(abs);
            line.LineWidth = 2;
            line.Color = SPColor.FromHex(PinColors[pinCounter % PinColors.Length]);

            pins.Add(new Pin
            {
                Index = abs,
                Y = snapY[i],
                Name = ((char)('A' + pinCounter % 26)).ToString(),
                Line = line
            });
            pinCounter++;

            if (pins.Count > MaxPins)
            {
                plotControl.Plot.Remove(pins[0].Line);
                pins.RemoveAt(0);
            }

            UpdatePinText();
            plotControl.Refresh();
        }

        private void UpdatePinText()
        {
            if (pins.Count == 0)
            {
                pinText.Text = "Click on the chart to pin a line and compare.";
                return;
            }

            string text = string.Join("    ",
                pins.Select(p => string.Format(CultureInfo.InvariantCulture, "{0}: #{1} = {2:0.0}", p.Name, p.Index, p.Y)));

            if (pins.Count >= 2)
            {
                Pin a = pins[pins.Count - 2];
                Pin b = pins[pins.Count - 1];
                text += string.Format(CultureInfo.InvariantCulture,
                    "\nΔ {0}→{1}: {2} samples, ΔPPG = {3:+0.0;-0.0;0.0}",
                    a.Name, b.Name, b.Index - a.Index, b.Y - a.Y);
            }
            pinText.Text = text;
        }

        private void RemoveAllPins()
        {
            foreach (Pin p in pins) plotControl.Plot.Remove(p.Line);
            pins.Clear();
            pinCounter = 0;
            UpdatePinText();
        }
    }
}
