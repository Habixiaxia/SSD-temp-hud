using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace SsdTempHud
{
    /// <summary>
    /// 顶部悬浮横条窗口：无边框、置顶、不占任务栏、不抢焦点、可点击穿透。
    /// </summary>
    internal sealed class HudWindow : Window
    {
        private const double BarHeight = 34.0;

        private readonly StackPanel _panel;
        private readonly Border _root;
        private readonly Dictionary<int, DiskView> _views = new Dictionary<int, DiskView>();
        private bool _clickThrough = true;
        private double _opacity = 0.75;

        public HudWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Focusable = false;
            ShowActivated = false;
            FontFamily = Theme.Font;

            _panel = new StackPanel();
            _panel.Orientation = Orientation.Horizontal;
            _panel.VerticalAlignment = VerticalAlignment.Center;

            TextBlock loading = new TextBlock();
            loading.Text = "正在读取磁盘…";
            loading.FontSize = 12;
            loading.Foreground = Theme.Muted;
            loading.VerticalAlignment = VerticalAlignment.Center;
            _panel.Children.Add(loading);

            _root = new Border();
            _root.Background = Theme.CreateHudBackground(_opacity);
            _root.BorderBrush = Theme.HudBorder;
            _root.BorderThickness = new Thickness(1, 0, 1, 1);
            _root.CornerRadius = new CornerRadius(0, 0, 10, 10);
            _root.Padding = new Thickness(14, 0, 14, 0);
            _root.Height = BarHeight;
            _root.Child = _panel;
            Content = _root;

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        public void SetClickThrough(bool enabled)
        {
            _clickThrough = enabled;
            ApplyClickThrough();
        }

        public void SetBackgroundOpacity(double opacity)
        {
            _opacity = opacity;
            _root.Background = Theme.CreateHudBackground(opacity);
        }

        public void UpdateDisks(IList<DiskSnapshot> disks)
        {
            bool rebuild = disks.Count != _views.Count;
            if (!rebuild)
            {
                for (int i = 0; i < disks.Count; i++)
                {
                    if (!_views.ContainsKey(disks[i].Index))
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
            {
                _panel.Children.Clear();
                _views.Clear();
                for (int i = 0; i < disks.Count; i++)
                {
                    if (i > 0)
                    {
                        Border separator = new Border();
                        separator.Width = 1;
                        separator.Height = 14;
                        separator.Background = Theme.Separator;
                        separator.Margin = new Thickness(14, 0, 14, 0);
                        separator.VerticalAlignment = VerticalAlignment.Center;
                        _panel.Children.Add(separator);
                    }

                    DiskView view = new DiskView();
                    _views[disks[i].Index] = view;
                    _panel.Children.Add(view.Root);
                }
                Reposition();
            }

            for (int i = 0; i < disks.Count; i++)
            {
                DiskView view;
                if (_views.TryGetValue(disks[i].Index, out view))
                {
                    view.Update(disks[i]);
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = new WindowInteropHelper(this).Handle;
            int style = Native.GetWindowLong(handle, Native.GWL_EXSTYLE);
            style |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            Native.SetWindowLong(handle, Native.GWL_EXSTYLE, style);
            StartupLog.Write("hud source initialized: handle=0x" + handle.ToInt64().ToString("X")
                + " exstyle=0x" + style.ToString("X"));
            ApplyClickThrough();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Reposition();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Reposition();
        }

        private void Reposition()
        {
            Rect area = SystemParameters.WorkArea;
            Left = Math.Round(area.Left + (area.Width - ActualWidth) / 2.0);
            Top = Math.Round(area.Top);
        }

        private void ApplyClickThrough()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            int style = Native.GetWindowLong(handle, Native.GWL_EXSTYLE);
            if (_clickThrough)
            {
                style |= Native.WS_EX_TRANSPARENT;
            }
            else
            {
                style &= ~Native.WS_EX_TRANSPARENT;
            }
            Native.SetWindowLong(handle, Native.GWL_EXSTYLE, style);
        }
    }
}
