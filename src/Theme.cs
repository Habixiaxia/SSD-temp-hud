using System;
using System.Windows;
using System.Windows.Media;

namespace SsdTempHud
{
    internal static class Theme
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI, Microsoft YaHei UI");

        public static readonly SolidColorBrush HudBorder = Frozen(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush Text = Frozen(Color.FromRgb(0xE6, 0xEA, 0xF2));
        public static readonly SolidColorBrush Muted = Frozen(Color.FromRgb(0x8B, 0x93, 0xA7));
        public static readonly SolidColorBrush Ok = Frozen(Color.FromRgb(0x4A, 0xDE, 0x80));
        public static readonly SolidColorBrush Warn = Frozen(Color.FromRgb(0xFB, 0xBF, 0x24));
        public static readonly SolidColorBrush Hot = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));
        public static readonly SolidColorBrush None = Frozen(Color.FromRgb(0x6B, 0x72, 0x80));
        public static readonly SolidColorBrush Accent = Frozen(Color.FromRgb(0x38, 0xBD, 0xF8));
        public static readonly SolidColorBrush Track = Frozen(Color.FromRgb(0x1E, 0x29, 0x3B));
        public static readonly SolidColorBrush Separator = Frozen(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

        /// <summary>背景色（深色 R=13 G=16 B=22），按 0–1 的不透明度生成。</summary>
        public static SolidColorBrush CreateHudBackground(double opacity)
        {
            double clamped = Math.Max(0.0, Math.Min(1.0, opacity));
            byte alpha = (byte)Math.Round(clamped * 255.0);
            return Frozen(Color.FromArgb(alpha, 0x0D, 0x10, 0x16));
        }

        private static SolidColorBrush Frozen(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
