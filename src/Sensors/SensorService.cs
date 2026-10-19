using System;
using System.Collections.Generic;
using System.Threading;

namespace SsdTempHud.Sensors
{
    /// <summary>
    /// 传感器调度：启动时同步采集一次，之后每 1 秒采活动率、每 3 秒采温度、每 60 秒刷新元数据。
    /// 所有采集都在后台线程，快照通过事件交给 UI 线程。
    /// </summary>
    internal sealed class SensorService : IDisposable
    {
        private readonly ActivitySensor _activity = new ActivitySensor();
        private readonly TemperatureSensor _temperature = new TemperatureSensor();
        private readonly DiskCatalog _catalog = new DiskCatalog();

        private Timer _timer;
        private int _tick;
        private int _busy;
        private bool _disposed;

        public event Action<IList<DiskSnapshot>> SnapshotReady;

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(delegate { Initialize(); });
        }

        private void Initialize()
        {
            if (_disposed) return;
            try
            {
                _catalog.Refresh();
                _activity.RefreshInstances();
                _activity.Sample();
                SampleTemperatures();
                Publish();

                if (_disposed) return;
                _timer = new Timer(OnTick, null, 1000, 1000);
            }
            catch (Exception ex)
            {
                StartupLog.Write("sensor init: " + ex.Message);
            }
        }

        private void OnTick(object state)
        {
            if (_disposed) return;
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                _tick++;
                _activity.Sample();
                if (_tick % 3 == 0)
                {
                    SampleTemperatures();
                }
                if (_tick % 60 == 0)
                {
                    _catalog.Refresh();
                    _activity.RefreshInstances();
                    _activity.Sample();
                    SampleTemperatures();
                }
                Publish();
            }
            catch (Exception ex)
            {
                StartupLog.Write("sensor tick: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void SampleTemperatures()
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < _activity.Disks.Count; i++)
            {
                indexes.Add(_activity.Disks[i].Index);
            }
            if (indexes.Count == 0)
            {
                for (int i = 0; i < 8; i++) indexes.Add(i);
            }
            _temperature.Sample(indexes);
        }

        private void Publish()
        {
            IList<DiskSnapshot> snapshot = Build();
            Action<IList<DiskSnapshot>> handler = SnapshotReady;
            if (handler != null) handler(snapshot);
        }

        private IList<DiskSnapshot> Build()
        {
            List<DiskSnapshot> list = new List<DiskSnapshot>();

            if (_activity.Disks.Count > 0)
            {
                for (int i = 0; i < _activity.Disks.Count; i++)
                {
                    ActivityDisk disk = _activity.Disks[i];
                    DiskSnapshot snapshot = new DiskSnapshot();
                    snapshot.Index = disk.Index;
                    snapshot.Letters = disk.Letters;
                    snapshot.Model = _catalog.ModelFor(disk.Index);
                    snapshot.ActivityAvailable = disk.Available;
                    snapshot.ActivityPercent = disk.Percent;

                    double celsius;
                    if (_temperature.TryGet(disk.Index, out celsius))
                    {
                        snapshot.TemperatureC = celsius;
                    }
                    snapshot.Health = (disk.Available || snapshot.TemperatureC.HasValue)
                        ? DiskHealth.Ok : DiskHealth.Unavailable;
                    list.Add(snapshot);
                }
                return list;
            }

            foreach (int index in _temperature.Indexes)
            {
                double celsius;
                if (!_temperature.TryGet(index, out celsius)) continue;

                DiskSnapshot snapshot = new DiskSnapshot();
                snapshot.Index = index;
                snapshot.Model = _catalog.ModelFor(index);
                snapshot.TemperatureC = celsius;
                snapshot.ActivityAvailable = false;
                snapshot.Health = DiskHealth.Ok;
                list.Add(snapshot);
            }
            return list;
        }

        public void Dispose()
        {
            _disposed = true;
            Timer timer = _timer;
            _timer = null;
            if (timer != null) timer.Dispose();
            _activity.Dispose();
        }
    }
}
