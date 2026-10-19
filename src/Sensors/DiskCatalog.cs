using System;
using System.Collections.Generic;
using System.Management;

namespace SsdTempHud.Sensors
{
    /// <summary>
    /// 磁盘型号元数据。优先使用 Windows 存储命名空间，失败时回退 Win32_DiskDrive。
    /// 只用于 tooltip 显示，失败不影响核心功能。
    /// </summary>
    internal sealed class DiskCatalog
    {
        private readonly Dictionary<int, string> _models = new Dictionary<int, string>();
        private readonly object _sync = new object();

        public string ModelFor(int index)
        {
            lock (_sync)
            {
                string model;
                return _models.TryGetValue(index, out model) ? model : "";
            }
        }

        public void Refresh()
        {
            Dictionary<int, string> next = new Dictionary<int, string>();
            try
            {
                QueryStorage(next);
            }
            catch (Exception ex)
            {
                StartupLog.Write("catalog storage: " + ex.Message);
            }

            if (next.Count == 0)
            {
                try
                {
                    QueryWin32(next);
                }
                catch (Exception ex)
                {
                    StartupLog.Write("catalog win32: " + ex.Message);
                }
            }

            lock (_sync)
            {
                _models.Clear();
                foreach (KeyValuePair<int, string> pair in next)
                {
                    _models[pair.Key] = pair.Value;
                }
            }
        }

        private static void QueryStorage(Dictionary<int, string> target)
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT Number, FriendlyName FROM MSFT_Disk")))
            {
                searcher.Options.Timeout = TimeSpan.FromSeconds(3);
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject disk in results)
                    {
                        try
                        {
                            object number = disk["Number"];
                            object name = disk["FriendlyName"];
                            if (number == null || name == null) continue;
                            target[Convert.ToInt32(number)] = Shorten(Convert.ToString(name));
                        }
                        finally
                        {
                            disk.Dispose();
                        }
                    }
                }
            }
        }

        private static void QueryWin32(Dictionary<int, string> target)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT Index, Model FROM Win32_DiskDrive"))
            {
                searcher.Options.Timeout = TimeSpan.FromSeconds(3);
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject disk in results)
                    {
                        try
                        {
                            object index = disk["Index"];
                            object model = disk["Model"];
                            if (index == null || model == null) continue;
                            target[Convert.ToInt32(index)] = Shorten(Convert.ToString(model));
                        }
                        finally
                        {
                            disk.Dispose();
                        }
                    }
                }
            }
        }

        private static string Shorten(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string text = name.Trim();
            if (text.StartsWith("NVMe ", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(5);
            }
            if (text.Length > 18)
            {
                text = text.Substring(0, 17) + "\u2026";
            }
            return text;
        }
    }
}
