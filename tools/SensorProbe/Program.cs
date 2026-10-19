using System;
using System.Collections.Generic;
using System.Threading;
using SsdTempHud.Sensors;

namespace SsdTempHud
{
    internal static class SensorProbeProgram
    {
        private static IList<DiskSnapshot> _last;
        private static readonly object Sync = new object();

        public static void Main()
        {
            Console.WriteLine("sensor probe start");

            SensorService service = new SensorService();
            service.SnapshotReady += delegate(IList<DiskSnapshot> list)
            {
                lock (Sync)
                {
                    _last = list;
                }
            };
            service.Start();

            // 制造持续写入，验证活动率采样是否跟随
            Thread load = new Thread(delegate()
            {
                try
                {
                    string path = @"F:\daily\_sensor_probe_tmp.bin";
                    using (System.IO.FileStream stream = new System.IO.FileStream(path,
                        System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read, 65536))
                    {
                        byte[] buffer = new byte[65536];
                        DateTime end = DateTime.UtcNow.AddSeconds(4);
                        while (DateTime.UtcNow < end)
                        {
                            stream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    System.IO.File.Delete(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("load error: " + ex.Message);
                }
            });
            load.IsBackground = true;
            load.Start();

            Thread.Sleep(5000);

            lock (Sync)
            {
                if (_last == null || _last.Count == 0)
                {
                    Console.WriteLine("RESULT: no disks");
                }
                else
                {
                    foreach (DiskSnapshot disk in _last)
                    {
                        Console.WriteLine("RESULT disk=" + disk.Index
                            + " letters=" + disk.Letters
                            + " model=" + disk.Model
                            + " temp=" + (disk.TemperatureC.HasValue ? disk.TemperatureC.Value.ToString("F0") : "--")
                            + " activity=" + (disk.ActivityAvailable ? disk.ActivityPercent.ToString("F1") : "--")
                            + " health=" + disk.Health);
                    }
                }
            }

            service.Dispose();
            Console.WriteLine("sensor probe done");
        }
    }
}
