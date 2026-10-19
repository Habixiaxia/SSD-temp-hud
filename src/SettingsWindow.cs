using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SsdTempHud
{
    /// <summary>
    /// 普通设置窗口：会出现在任务栏与 Alt+Tab 中，作为可见的交互入口。
    /// </summary>
    internal sealed class SettingsWindow : Window
    {
        public event Action<double> OpacityChanged;
        public event Action<bool> ClickThroughChanged;
        public event Action<bool> VisibilityChanged;

        private readonly Slider _opacitySlider;
        private readonly TextBlock _opacityValue;
        private readonly CheckBox _clickThroughBox;
        private readonly CheckBox _visibleBox;
        private bool _initializing;

        public SettingsWindow(bool visible, bool clickThrough, double opacity, bool administrator)
        {
            Title = "盘温 · 设置";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
            Background = Brush(0xF7, 0xF8, 0xFA);
            FontFamily = Theme.Font;
            FontSize = 12;
            Icon = LoadWindowIcon();

            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(22, 18, 22, 18);

            TextBlock title = new TextBlock();
            title.Text = "盘温";
            title.FontSize = 16;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Brush(0x11, 0x18, 0x27);
            panel.Children.Add(title);

            TextBlock subtitle = new TextBlock();
            subtitle.Text = "SSD 温度悬浮条 · 横条显示在屏幕顶部，不占任务栏。";
            subtitle.Margin = new Thickness(0, 6, 0, 18);
            subtitle.TextWrapping = TextWrapping.Wrap;
            subtitle.Foreground = Brush(0x6B, 0x72, 0x80);
            panel.Children.Add(subtitle);

            _opacityValue = new TextBlock();
            _opacityValue.Text = ((int)Math.Round(opacity * 100.0)).ToString() + "%";
            _opacityValue.HorizontalAlignment = HorizontalAlignment.Right;
            _opacityValue.Foreground = Brush(0x37, 0x41, 0x51);

            Grid opacityHeader = new Grid();
            TextBlock opacityLabel = new TextBlock();
            opacityLabel.Text = "背景不透明度";
            opacityLabel.Foreground = Brush(0x37, 0x41, 0x51);
            opacityHeader.Children.Add(opacityLabel);
            opacityHeader.Children.Add(_opacityValue);
            panel.Children.Add(opacityHeader);

            _opacitySlider = new Slider();
            _opacitySlider.Minimum = 0;
            _opacitySlider.Maximum = 100;
            _opacitySlider.TickFrequency = 5;
            _opacitySlider.IsSnapToTickEnabled = true;
            _opacitySlider.Value = Math.Round(opacity * 100.0);
            _opacitySlider.Margin = new Thickness(0, 4, 0, 6);
            panel.Children.Add(_opacitySlider);

            TextBlock opacityHint = new TextBlock();
            opacityHint.Text = "0% = 完全透明，只显示文字；拖动即时生效";
            opacityHint.Foreground = Brush(0x8B, 0x93, 0xA7);
            opacityHint.Margin = new Thickness(0, 0, 0, 16);
            panel.Children.Add(opacityHint);

            _clickThroughBox = new CheckBox();
            _clickThroughBox.Content = "点击穿透（游戏鼠标直接穿过横条）";
            _clickThroughBox.IsChecked = clickThrough;
            _clickThroughBox.Margin = new Thickness(0, 0, 0, 10);
            panel.Children.Add(_clickThroughBox);

            _visibleBox = new CheckBox();
            _visibleBox.Content = "显示横条";
            _visibleBox.IsChecked = visible;
            _visibleBox.Margin = new Thickness(0, 0, 0, 18);
            panel.Children.Add(_visibleBox);

            TextBlock hint = new TextBlock();
            hint.Text = "热键：Ctrl+Alt+H 显示/隐藏　Ctrl+Alt+M 切换穿透";
            hint.Foreground = Brush(0x8B, 0x93, 0xA7);
            hint.TextWrapping = TextWrapping.Wrap;
            panel.Children.Add(hint);

            TextBlock version = new TextBlock();
            version.Text = "M2 · 实时监控（温度 / 活动率）";
            version.Margin = new Thickness(0, 4, 0, 0);
            version.Foreground = Brush(0x8B, 0x93, 0xA7);
            panel.Children.Add(version);

            TextBlock rights = new TextBlock();
            rights.Text = administrator ? "权限：管理员（温度可读）" : "权限：普通用户（温度不可读）";
            rights.Margin = new Thickness(0, 4, 0, 0);
            rights.Foreground = administrator ? Brush(0x16, 0x7A, 0x3E) : Brush(0xB4, 0x3C, 0x3C);
            panel.Children.Add(rights);

            Button close = new Button();
            close.Content = "关闭";
            close.Width = 84;
            close.Height = 28;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 20, 0, 0);
            close.IsCancel = true;
            close.Click += delegate { Close(); };
            panel.Children.Add(close);

            Content = panel;

            _opacitySlider.ValueChanged += delegate
            {
                _opacityValue.Text = ((int)Math.Round(_opacitySlider.Value)).ToString() + "%";
                if (!_initializing && OpacityChanged != null)
                {
                    OpacityChanged(_opacitySlider.Value / 100.0);
                }
            };
            _clickThroughBox.Checked += delegate
            {
                if (!_initializing && ClickThroughChanged != null) ClickThroughChanged(true);
            };
            _clickThroughBox.Unchecked += delegate
            {
                if (!_initializing && ClickThroughChanged != null) ClickThroughChanged(false);
            };
            _visibleBox.Checked += delegate
            {
                if (!_initializing && VisibilityChanged != null) VisibilityChanged(true);
            };
            _visibleBox.Unchecked += delegate
            {
                if (!_initializing && VisibilityChanged != null) VisibilityChanged(false);
            };
        }

        public void SyncOpacity(double opacity)
        {
            _initializing = true;
            _opacitySlider.Value = Math.Round(opacity * 100.0);
            _opacityValue.Text = ((int)Math.Round(opacity * 100.0)).ToString() + "%";
            _initializing = false;
        }

        public void SyncClickThrough(bool enabled)
        {
            _initializing = true;
            _clickThroughBox.IsChecked = enabled;
            _initializing = false;
        }

        public void SyncVisible(bool visible)
        {
            _initializing = true;
            _visibleBox.IsChecked = visible;
            _initializing = false;
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static ImageSource LoadWindowIcon()
        {
            try
            {
                string path = System.Windows.Forms.Application.ExecutablePath;
                using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;
                    return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
