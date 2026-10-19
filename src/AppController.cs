using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SsdTempHud.Sensors;

namespace SsdTempHud
{
    /// <summary>
    /// 应用状态与命令：窗口、托盘、热键、设置窗口、传感器。
    /// </summary>
    internal sealed class AppController
    {
        private HudWindow _window;
        private TrayIcon _tray;
        private HotkeyManager _hotkeys;
        private SensorService _sensors;
        private SettingsWindow _settings;
        private MenuItem _throughMenuItem;

        private bool _visible = true;
        private bool _clickThrough = true;
        private double _opacity = 0.75;
        private bool _administrator;

        public void Start()
        {
            _administrator = IsAdministrator();
            StartupLog.Write("start: user=" + Environment.UserName
                + " session=" + Process.GetCurrentProcess().SessionId
                + " admin=" + _administrator);

            _window = new HudWindow();
            _window.SetClickThrough(_clickThrough);
            _window.SetBackgroundOpacity(_opacity);
            _window.Show();

            IntPtr handle = new WindowInteropHelper(_window).Handle;
            StartupLog.Write("hud: visible=" + _window.IsVisible
                + " handle=0x" + handle.ToInt64().ToString("X"));

            _sensors = new SensorService();
            _sensors.SnapshotReady += OnSnapshotReady;
            _sensors.Start();

            BuildContextMenu();

            _tray = new TrayIcon();
            _tray.SettingsRequested += OpenSettings;
            _tray.ToggleVisibilityRequested += ToggleVisibility;
            _tray.ToggleClickThroughRequested += ToggleClickThrough;
            _tray.OpacityRequested += SetOpacity;
            _tray.ExitRequested += Exit;
            _tray.SetOpacityState(_opacity);
            StartupLog.Write("tray: created");

            if (!_administrator)
            {
                _tray.ShowInfo("温度读取需要管理员权限，当前只能显示活动率。请退出后以管理员身份运行。");
            }

            _hotkeys = new HotkeyManager();
            int error;
            bool h1 = _hotkeys.Register(1, Native.MOD_CONTROL | Native.MOD_ALT, Key.H, ToggleVisibility, out error);
            StartupLog.Write("hotkey H: ok=" + h1 + " err=" + error);
            bool h2 = _hotkeys.Register(2, Native.MOD_CONTROL | Native.MOD_ALT, Key.M, ToggleClickThrough, out error);
            StartupLog.Write("hotkey M: ok=" + h2 + " err=" + error);

            if (!h1 && !h2)
            {
                SetClickThrough(false);
                StartupLog.Write("no hotkeys available; fallback to interactive mode");
                _tray.ShowInfo("热键被占用，已临时关闭点击穿透。可从托盘菜单操作。");
            }

            StartupLog.Write("started");
        }

        public void Stop()
        {
            if (_sensors != null)
            {
                _sensors.Dispose();
                _sensors = null;
            }
            if (_hotkeys != null)
            {
                _hotkeys.Dispose();
                _hotkeys = null;
            }
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
            StartupLog.Write("stopped");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void OnSnapshotReady(IList<DiskSnapshot> disks)
        {
            Application app = Application.Current;
            if (app == null) return;
            try
            {
                app.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (_window != null) _window.UpdateDisks(disks);
                }));
            }
            catch
            {
                // 关闭过程中忽略
            }
        }

        private void BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem settingsItem = new MenuItem();
            settingsItem.Header = "设置…";
            settingsItem.Click += delegate { OpenSettings(); };

            _throughMenuItem = new MenuItem();
            _throughMenuItem.Header = "点击穿透";
            _throughMenuItem.IsCheckable = true;
            _throughMenuItem.IsChecked = _clickThrough;
            _throughMenuItem.Click += delegate { ToggleClickThrough(); };

            MenuItem exitItem = new MenuItem();
            exitItem.Header = "退出";
            exitItem.Click += delegate { Exit(); };

            menu.Items.Add(settingsItem);
            menu.Items.Add(_throughMenuItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);
            _window.ContextMenu = menu;
        }

        private void OpenSettings()
        {
            if (_settings != null)
            {
                _settings.Activate();
                return;
            }

            SettingsWindow settings = new SettingsWindow(_visible, _clickThrough, _opacity, _administrator);
            settings.OpacityChanged += SetOpacity;
            settings.ClickThroughChanged += SetClickThrough;
            settings.VisibilityChanged += SetVisibility;
            settings.Closed += delegate
            {
                _settings = null;
            };
            _settings = settings;
            settings.Show();
            StartupLog.Write("settings opened");
        }

        private void ToggleVisibility()
        {
            SetVisibility(!_visible);
        }

        private void SetVisibility(bool visible)
        {
            _visible = visible;
            if (visible)
            {
                _window.Show();
                _window.Topmost = true;
            }
            else
            {
                _window.Hide();
            }
            _tray.SetVisibleState(visible);
            if (_settings != null) _settings.SyncVisible(visible);
        }

        private void ToggleClickThrough()
        {
            SetClickThrough(!_clickThrough);
        }

        private void SetClickThrough(bool enabled)
        {
            _clickThrough = enabled;
            _window.SetClickThrough(enabled);
            _tray.SetClickThroughState(enabled);
            if (_throughMenuItem != null) _throughMenuItem.IsChecked = enabled;
            if (_settings != null) _settings.SyncClickThrough(enabled);
        }

        private void SetOpacity(double opacity)
        {
            _opacity = opacity;
            _window.SetBackgroundOpacity(opacity);
            _tray.SetOpacityState(opacity);
            if (_settings != null) _settings.SyncOpacity(opacity);
            StartupLog.Write("opacity=" + opacity);
        }

        private void Exit()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate
            {
                Application.Current.Shutdown();
            }));
        }
    }
}
