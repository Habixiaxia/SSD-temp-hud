using System;
using System.Collections.Generic;

namespace SsdTempHud.Sensors
{
    /// <summary>
    /// 温度缓存：读取成功时更新；30 秒内没有新值则视为失效。
    /// 采样在后台线程，读取在同一个线程，因此不加锁。
    /// </summary>
    internal sealed class TemperatureSensor
    {
        private sealed class Entry
        {
            public double Celsius;
            public DateTime SampledAt;
        }

        private readonly Dictionary<int, Entry> _values = new Dictionary<int, Entry>();
        private readonly HashSet<int> _logged = new HashSet<int>();

        public IEnumerable<int> Indexes
        {
            get { return _values.Keys; }
        }

        public void Sample(IEnumerable<int> indexes)
        {
            foreach (int index in indexes)
            {
                double celsius;
                int error;
                if (TemperatureReader.TryRead(index, out celsius, out error))
                {
                    Entry entry = new Entry();
                    entry.Celsius = celsius;
                    entry.SampledAt = DateTime.UtcNow;
                    _values[index] = entry;
                }
                else if (_logged.Add(index))
                {
                    StartupLog.Write("temperature: disk " + index + " failed err=" + error);
                }
            }
        }

        public bool TryGet(int index, out double celsius)
        {
            celsius = 0.0;
            Entry entry;
            if (!_values.TryGetValue(index, out entry))
            {
                return false;
            }
            if ((DateTime.UtcNow - entry.SampledAt).TotalSeconds > 30.0)
            {
                return false;
            }
            celsius = entry.Celsius;
            return true;
        }
    }
}
