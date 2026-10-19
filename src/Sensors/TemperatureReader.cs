using System;
using System.Runtime.InteropServices;

namespace SsdTempHud.Sensors
{
    /// <summary>
    /// 通过 Windows 存储接口读取 NVMe 硬盘温度。
    /// 返回结构：STORAGE_TEMPERATURE_DATA_DESCRIPTOR，温度位于 24 + 2（short，摄氏度）。
    /// </summary>
    internal static class TemperatureReader
    {
        private const uint IoctlStorageQueryProperty = 0x2D1400;
        private const int StorageDeviceTemperatureProperty = 52;

        public static bool TryRead(int diskIndex, out double celsius, out int error)
        {
            celsius = 0.0;
            error = 0;

            IntPtr handle = Native.OpenPhysicalDrive(diskIndex, Native.GENERIC_READ);
            if (handle == new IntPtr(-1))
            {
                handle = Native.OpenPhysicalDrive(diskIndex, Native.GENERIC_READ | Native.GENERIC_WRITE);
            }
            if (handle == new IntPtr(-1))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                byte[] query = new byte[12];
                Array.Copy(BitConverter.GetBytes(StorageDeviceTemperatureProperty), 0, query, 0, 4);

                byte[] output = new byte[512];
                int returned;
                bool ok = Native.DeviceIoControl(handle, IoctlStorageQueryProperty, query, query.Length,
                    output, output.Length, out returned, IntPtr.Zero);
                if (!ok)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }
                if (returned < 40)
                {
                    error = -1;
                    return false;
                }

                int count = BitConverter.ToUInt16(output, 12);
                if (count <= 0)
                {
                    error = -2;
                    return false;
                }

                short temperature = BitConverter.ToInt16(output, 26);
                if (temperature <= 0 || temperature > 120)
                {
                    error = -3;
                    return false;
                }

                celsius = temperature;
                return true;
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }
    }
}
