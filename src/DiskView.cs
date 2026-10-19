using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SsdTempHud
{
    /// <summary>
    /// 单块磁盘的视觉单元：状态点 / 盘符 / 温度 / 活动率条 / 百分比。
    /// </summary>
    internal sealed class DiskView
    {
        private const double BarWidth = 44.0;

        private readonly TextBlock _letters;
        private readonly TextBlock _temperature;
        private readonly TextBlock _percent;
        private readonly Rectangle _fill;
        private readonly Ellipse _dot;

        public FrameworkElement Root { get; private set; }

        public DiskView()
        {
            _dot = new Ellipse();
            _dot.Width = 6;
            _dot.Height = 6;
            _dot.Fill = Theme.Ok;
            _dot.VerticalAlignment = VerticalAlignment.Center;
            _dot.Margin = new Thickness(0, 0, 6, 0);

            _letters = MakeText("--", 12, FontWeights.SemiBold, Theme.Text);
            _letters.MinWidth = 40;
            _letters.Margin = new Thickness(0, 0, 7, 0);

            _temperature = MakeText("--", 13, FontWeights.Bold, Theme.None);
            _temperature.MinWidth = 38;
            _temperature.TextAlignment = TextAlignment.Right;
            _temperature.Margin = new Thickness(0, 0, 8, 0);

            Rectangle track = new Rectangle();
            track.Width = BarWidth;
            track.Height = 4;
            track.RadiusX = 2;
            track.RadiusY = 2;
            track.Fill = Theme.Track;

            _fill = new Rectangle();
            _fill.Width = 0;
            _fill.Height = 4;
            _fill.RadiusX = 2;
            _fill.RadiusY = 2;
            _fill.Fill = Theme.Accent;
            _fill.HorizontalAlignment = HorizontalAlignment.Left;

            Grid bar = new Grid();
            bar.Width = BarWidth;
            bar.Height = 4;
            bar.VerticalAlignment = VerticalAlignment.Center;
            bar.Margin = new Thickness(0, 0, 7, 0);
            bar.Children.Add(track);
            bar.Children.Add(_fill);

            _percent = MakeText("--", 11, FontWeights.Normal, Theme.Muted);
            _percent.Width = 32;
            _percent.TextAlignment = TextAlignment.Right;

            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(_dot);
            panel.Children.Add(_letters);
            panel.Children.Add(_temperature);
            panel.Children.Add(bar);
            panel.Children.Add(_percent);

            Root = panel;
            ToolTipService.SetInitialShowDelay(panel, 300);
            ToolTipService.SetShowDuration(panel, 20000);
        }

        public void Update(DiskSnapshot disk)
        {
            if (disk == null) return;

            Root.ToolTip = BuildToolTip(disk);
            _letters.Text = string.IsNullOrEmpty(disk.Letters) ? "Disk " + disk.Index : disk.Letters;

            if (disk.Health == DiskHealth.Unavailable)
            {
                _temperature.Text = "--";
                _temperature.Foreground = Theme.None;
                _percent.Text = "--";
                _fill.Width = 0;
                _dot.Fill = Theme.None;
                return;
            }

            if (disk.TemperatureC.HasValue)
            {
                double celsius = disk.TemperatureC.Value;
                _temperature.Text = ((int)Math.Round(celsius)).ToString() + "\u00B0C";
                _temperature.Foreground = TemperatureBrush(celsius);
            }
            else
            {
                _temperature.Text = "--";
                _temperature.Foreground = Theme.None;
            }

            if (disk.ActivityAvailable)
            {
                double pct = disk.ActivityPercent;
                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;
                _percent.Text = ((int)Math.Round(pct)).ToString() + "%";
                _fill.Width = BarWidth * pct / 100.0;
            }
            else
            {
                _percent.Text = "--";
                _fill.Width = 0;
            }

            _dot.Fill = disk.Health == DiskHealth.Stale ? Theme.Warn : Theme.Ok;
        }

        private static string BuildToolTip(DiskSnapshot disk)
        {
            string text = string.IsNullOrEmpty(disk.Model) ? "Disk " + disk.Index : disk.Model;
            if (!string.IsNullOrEmpty(disk.Letters))
            {
                text += "  (" + disk.Letters + ")";
            }
            if (disk.TemperatureC.HasValue)
            {
                text += "\n温度 " + ((int)Math.Round(disk.TemperatureC.Value)).ToString() + "\u00B0C";
            }
            else
            {
                text += "\n温度 不可用";
            }
            if (disk.ActivityAvailable)
            {
                text += " · 活动 " + ((int)Math.Round(disk.ActivityPercent)).ToString() + "%";
            }
            text += "\n关闭点击穿透后可交互";
            return text;
        }

        private static Brush TemperatureBrush(double celsius)
        {
            if (celsius >= 75.0) return Theme.Hot;
            if (celsius >= 65.0) return Theme.Warn;
            return Theme.Ok;
        }

        private static TextBlock MakeText(string text, double size, FontWeight weight, Brush brush)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.FontWeight = weight;
            block.Foreground = brush;
            block.VerticalAlignment = VerticalAlignment.Center;
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(block, TextRenderingMode.Grayscale);
            return block;
        }
    }
}
