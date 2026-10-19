using System;
using System.IO;

namespace SsdTempHud
{
    /// <summary>
    /// 启动诊断日志。写到程序目录下的 logs\startup.log，
    /// 避免用户配置目录权限或重定向导致日志丢失。
    /// </summary>
    internal static class StartupLog
    {
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs", "startup.log");

        public static void Write(string message)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch
            {
                // 日志失败不能影响主流程
            }
        }
    }
}
