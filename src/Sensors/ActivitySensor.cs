using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SsdTempHud.Sensors
{
    internal sealed class ActivityDisk
    {
        public int Index;
        public string Letters;
        public double Percent;
        public bool Available;
    }

    /// <summary>
    /// 磁盘活动率：物理磁盘性能计数器 "% Disk Time"。
    /// 实例名形如 "0 C: D:"，前缀数字即物理磁盘序号。
    /// </summary>
    internal sealed class ActivitySensor : IDisposable
    {
        private readonly List<ActivityDisk> _disks = new List<ActivityDisk>();
        private readonly Dictionary<int, PerformanceCounter> _counters = new Dictionary<int, PerformanceCounter>();

        public IList<ActivityDisk> Disks
        {
            get { return _disks; }
        }

        public void RefreshInstances()
        {
            foreach (PerformanceCounter counter in _counters.Values)
            {
                try { counter.Dispose(); } catch { }
            }
            _counters.Clear();
            _disks.Clear();

            try
            {
                PerformanceCounterCategory category = new PerformanceCounterCategory("PhysicalDisk");
                string[] instances = category.GetInstanceNames();
                for (int i = 0; i < instances.Length; i++)
                {
                    string name = instances[i];
                    if (name == "_Total") continue;

                    int index;
                    string letters;
                    if (!ParseInstance(name, out index, out letters)) continue;

                    ActivityDisk disk = new ActivityDisk();
                    disk.Index = index;
                    disk.Letters = letters;
                    disk.Available = true;
                    _disks.Add(disk);

                    PerformanceCounter counter = new PerformanceCounter("PhysicalDisk", "% Disk Time", name, true);
                    counter.NextValue(); // 预热，第一次采样没有区间值
                    _counters[index] = counter;
                }
                _disks.Sort(delegate(ActivityDisk a, ActivityDisk b) { return a.Index.CompareTo(b.Index); });
                StartupLog.Write("activity: " + _disks.Count + " disks");
            }
            catch (Exception ex)
            {
                StartupLog.Write("activity init failed: " + ex.Message);
            }
        }

        public void Sample()
        {
            for (int i = 0; i < _disks.Count; i++)
            {
                ActivityDisk disk = _disks[i];
                PerformanceCounter counter;
                if (!_counters.TryGetValue(disk.Index, out counter))
                {
                    disk.Available = false;
                    continue;
                }
                try
                {
                    float value = counter.NextValue();
                    if (value < 0.0f) value = 0.0f;
                    if (value > 100.0f) value = 100.0f;
                    disk.Percent = value;
                    disk.Available = true;
                }
                catch
                {
                    disk.Available = false;
                }
            }
        }

        private static bool ParseInstance(string name, out int index, out string letters)
        {
            index = 0;
            letters = "";
            if (string.IsNullOrEmpty(name)) return false;

            int space = name.IndexOf(' ');
            string head = space < 0 ? name : name.Substring(0, space);
            if (!int.TryParse(head, out index)) return false;
            if (space >= 0) letters = name.Substring(space + 1).Trim();
            return true;
        }

        public void Dispose()
        {
            foreach (PerformanceCounter counter in _counters.Values)
            {
                try { counter.Dispose(); } catch { }
            }
            _counters.Clear();
            _disks.Clear();
        }
    }
}
