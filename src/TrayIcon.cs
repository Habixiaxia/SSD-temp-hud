using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SsdTempHud
{
    /// <summary>
    /// 托盘图标与右键菜单：设置、显示/隐藏、点击穿透、背景不透明度、退出。
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _visibleItem;
        private readonly ToolStripMenuItem _throughItem;
        private readonly ToolStripMenuItem _opacityMenu;
        private readonly List<ToolStripMenuItem> _opacityItems = new List<ToolStripMenuItem>();

        public event Action SettingsRequested;
        public event Action ToggleVisibilityRequested;
        public event Action ToggleClickThroughRequested;
        public event Action<double> OpacityRequested;
        public event Action ExitRequested;

        public TrayIcon()
        {
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置…");
            settingsItem.Font = new Font(settingsItem.Font, System.Drawing.FontStyle.Bold);
            settingsItem.Click += delegate { Raise(SettingsRequested); };

            _visibleItem = new ToolStripMenuItem("显示横条");
            _visibleItem.Checked = true;
            _visibleItem.Click += delegate { Raise(ToggleVisibilityRequested); };

            _throughItem = new ToolStripMenuItem("点击穿透");
            _throughItem.Checked = true;
            _throughItem.Click += delegate { Raise(ToggleClickThroughRequested); };

            _opacityMenu = new ToolStripMenuItem("背景不透明度");
            double[] presets = new double[] { 0.00, 0.25, 0.50, 0.75, 1.00 };
            for (int i = 0; i < presets.Length; i++)
            {
                double value = presets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(((int)Math.Round(value * 100.0)).ToString() + "%");
                item.Tag = value;
                item.Click += delegate
                {
                    double chosen = (double)item.Tag;
                    SetOpacityState(chosen);
                    if (OpacityRequested != null) OpacityRequested(chosen);
                };
                _opacityItems.Add(item);
                _opacityMenu.DropDownItems.Add(item);
            }

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { Raise(ExitRequested); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_visibleItem);
            menu.Items.Add(_throughItem);
            menu.Items.Add(_opacityMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = LoadAppIcon();
            _notifyIcon.Text = "盘温 · SSD 温度监控";
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { Raise(SettingsRequested); };
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch
            {
            }
            return SystemIcons.Application;
        }

        public void SetVisibleState(bool visible)
        {
            _visibleItem.Checked = visible;
        }

        public void SetClickThroughState(bool enabled)
        {
            _throughItem.Checked = enabled;
        }

        public void SetOpacityState(double opacity)
        {
            for (int i = 0; i < _opacityItems.Count; i++)
            {
                ToolStripMenuItem item = _opacityItems[i];
                item.Checked = Math.Abs((double)item.Tag - opacity) < 0.004;
            }
            _opacityMenu.Text = "背景不透明度  " + ((int)Math.Round(opacity * 100.0)).ToString() + "%";
        }

        public void ShowInfo(string message)
        {
            try
            {
                _notifyIcon.BalloonTipTitle = "SsdTempHud";
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(3000);
            }
            catch
            {
                // 气泡提示失败不影响主功能
            }
        }

        private static void Raise(Action action)
        {
            if (action != null) action();
        }

        public void Dispose()
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch
            {
            }
        }
    }
}
