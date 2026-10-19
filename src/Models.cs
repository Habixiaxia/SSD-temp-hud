namespace SsdTempHud
{
    internal enum DiskHealth
    {
        Ok,
        Stale,
        Unavailable
    }

    internal sealed class DiskSnapshot
    {
        public int Index;
        public string Letters = "";
        public string Model = "";
        public double? TemperatureC;
        public bool ActivityAvailable;
        public double ActivityPercent;
        public DiskHealth Health = DiskHealth.Ok;
    }
}
