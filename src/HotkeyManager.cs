using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SsdTempHud
{
    /// <summary>
    /// 全局热键。使用独立的消息窗口接收 WM_HOTKEY，
    /// 不依赖横条窗口是否创建/显示，便于诊断与恢复。
    /// </summary>
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HwndSource _source;
        private readonly IntPtr _handle;
        private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();

        public HotkeyManager()
        {
            HwndSourceParameters parameters = new HwndSourceParameters("SsdTempHudHotkeys");
            parameters.WindowStyle = 0;
            parameters.Width = 0;
            parameters.Height = 0;
            parameters.PositionX = -32000;
            parameters.PositionY = -32000;

            _source = new HwndSource(parameters);
            _handle = _source.Handle;
            _source.AddHook(WndProc);

            StartupLog.Write("hotkey window: handle=0x" + _handle.ToInt64().ToString("X"));
        }

        public bool Register(int id, uint modifiers, Key key, Action action, out int error)
        {
            _actions[id] = action;
            bool ok = Native.RegisterHotKey(_handle, id, modifiers | Native.MOD_NOREPEAT,
                (uint)KeyInterop.VirtualKeyFromKey(key));
            error = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                Action action;
                if (_actions.TryGetValue(id, out action))
                {
                    StartupLog.Write("hotkey fired: id=" + id);
                    action();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                foreach (int id in _actions.Keys)
                {
                    Native.UnregisterHotKey(_handle, id);
                }
                _source.RemoveHook(WndProc);
                _source.Dispose();
            }
            _actions.Clear();
        }
    }
}
