#Requires -Version 5.1
# SsdTempHud hardware probe.
# Read-only: enumerates disks, reads temperature/activity counters, writes probe-result.txt.
# Must run elevated (it will request elevation automatically).

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Lines = New-Object 'System.Collections.Generic.List[string]'

function Write-Line {
    param([string]$Text)
    $script:Lines.Add($Text)
    Write-Host $Text
}

function Write-Section {
    param([string]$Name)
    Write-Line ''
    Write-Line ('==== ' + $Name + ' ====')
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host 'Administrator rights are required. Requesting elevation...' -ForegroundColor Yellow
    $argLine = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"'
    try {
        Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argLine -ErrorAction Stop | Out-Null
    } catch {
        Write-Host ('Elevation cancelled: ' + $_.Exception.Message) -ForegroundColor Red
        Read-Host 'Press Enter to close'
    }
    exit
}

$cs = @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class DiskProbe
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, byte[] lpInBuffer,
        int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static IntPtr OpenDrive(int index)
    {
        string path = "\\\\.\\PhysicalDrive" + index;
        IntPtr h = CreateFileW(path, 0x80000000u, 3u, IntPtr.Zero, 3u, 0x80u, IntPtr.Zero);
        if (h == new IntPtr(-1))
        {
            h = CreateFileW(path, 0xC0000000u, 3u, IntPtr.Zero, 3u, 0x80u, IntPtr.Zero);
        }
        return h;
    }

    public static string Temp(int index)
    {
        IntPtr h = OpenDrive(index);
        if (h == new IntPtr(-1))
        {
            return "OPEN_FAIL err=" + Marshal.GetLastWin32Error();
        }
        try
        {
            byte[] query = new byte[12];
            Array.Copy(BitConverter.GetBytes(52), 0, query, 0, 4);
            Array.Copy(BitConverter.GetBytes(0), 0, query, 4, 4);
            byte[] output = new byte[2048];
            int returned = 0;
            bool ok = DeviceIoControl(h, 0x2D1400u, query, query.Length, output, output.Length, out returned, IntPtr.Zero);
            if (!ok)
            {
                return "TEMP_FAIL err=" + Marshal.GetLastWin32Error();
            }
            int version = BitConverter.ToInt32(output, 0);
            int size = BitConverter.ToInt32(output, 4);
            int critical = BitConverter.ToUInt16(output, 8);
            int warning = BitConverter.ToUInt16(output, 10);
            int count = BitConverter.ToUInt16(output, 12);
            StringBuilder sb = new StringBuilder();
            sb.Append("OK bytes=").Append(returned).Append(" ver=").Append(version)
              .Append(" size=").Append(size).Append(" count=").Append(count)
              .Append(" crit=").Append(critical).Append(" warn=").Append(warning).Append(" temps=");
            for (int i = 0; i < count; i++)
            {
                int off = 24 + i * 16;
                if (off + 4 > returned) { break; }
                short t = BitConverter.ToInt16(output, off + 2);
                sb.Append(t).Append(' ');
            }
            return sb.ToString();
        }
        finally
        {
            CloseHandle(h);
        }
    }

    public static string Perf(int index)
    {
        IntPtr h = OpenDrive(index);
        if (h == new IntPtr(-1))
        {
            return "OPEN_FAIL err=" + Marshal.GetLastWin32Error();
        }
        try
        {
            byte[] output = new byte[256];
            int returned = 0;
            bool ok = DeviceIoControl(h, 0x70021u, null, 0, output, output.Length, out returned, IntPtr.Zero);
            if (!ok)
            {
                return "PERF_FAIL err=" + Marshal.GetLastWin32Error();
            }
            long bytesRead = BitConverter.ToInt64(output, 0);
            long bytesWritten = BitConverter.ToInt64(output, 8);
            long idleTime = BitConverter.ToInt64(output, 32);
            long readCount = BitConverter.ToInt64(output, 40);
            long writeCount = BitConverter.ToInt64(output, 48);
            long queryTime = BitConverter.ToInt64(output, 72);
            return "OK bytes=" + returned + " read=" + bytesRead + " written=" + bytesWritten
                + " idle=" + idleTime + " query=" + queryTime + " rc=" + readCount + " wc=" + writeCount;
        }
        finally
        {
            CloseHandle(h);
        }
    }
}
'@

$resultPath = Join-Path $PSScriptRoot 'probe-result.txt'

try {
    Write-Line ('Time      : ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    Write-Line ('Computer  : ' + $env:COMPUTERNAME)
    Write-Line ('OS        : ' + [Environment]::OSVersion.VersionString)
    Write-Line ('PowerShell: ' + $PSVersionTable.PSVersion.ToString())
    Write-Line ('Admin     : True')

    Write-Section 'PHYSICAL DISKS'
    try {
        Get-PhysicalDisk -ErrorAction Stop | ForEach-Object {
            $sizeGb = 0
            if ($_.Size) { $sizeGb = [math]::Round([double]$_.Size / 1GB, 1) }
            Write-Line ('DeviceId=' + $_.DeviceId + ' Name=' + $_.FriendlyName + ' Media=' + $_.MediaType + ' Bus=' + $_.BusType + ' Health=' + $_.HealthStatus + ' SizeGB=' + $sizeGb)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'WIN32_DISKDRIVE AND LETTERS'
    try {
        $diskList = @(Get-CimInstance Win32_DiskDrive -ErrorAction Stop)
        foreach ($d in $diskList) {
            $letters = ''
            try {
                $parts = @(Get-Partition -DiskNumber ([int]$d.Index) -ErrorAction Stop)
                $letters = (($parts | Where-Object { $_.DriveLetter } | ForEach-Object { [string]$_.DriveLetter + ':' }) -join ',')
            } catch { }
            $sizeGb = 0
            if ($d.Size) { $sizeGb = [math]::Round([double]$d.Size / 1GB, 1) }
            Write-Line ('Index=' + $d.Index + ' Model=' + $d.Model + ' Interface=' + $d.InterfaceType + ' Media=' + $d.MediaType + ' SizeGB=' + $sizeGb + ' Letters=' + $letters + ' Serial=' + $d.SerialNumber)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'STORAGE RELIABILITY (Get-StorageReliabilityCounter)'
    try {
        Get-PhysicalDisk -ErrorAction Stop | ForEach-Object {
            $pd = $_
            try {
                $c = $pd | Get-StorageReliabilityCounter -ErrorAction Stop
                Write-Line ('DeviceId=' + $pd.DeviceId + ' Name=' + $pd.FriendlyName + ' Temperature=' + $c.Temperature + ' MaxTemp=' + $c.TemperatureMax + ' Wear=' + $c.Wear + ' PowerOnHours=' + $c.PowerOnHours + ' ReadErrors=' + $c.ReadErrorsTotal + ' WriteErrors=' + $c.WriteErrorsTotal)
            } catch {
                Write-Line ('DeviceId=' + $pd.DeviceId + ' COUNTER_ERROR ' + $_.Exception.Message)
            }
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'WMI MSFT_StorageReliabilityCounter'
    try {
        Get-CimInstance -Namespace 'root/Microsoft/Windows/Storage' -ClassName 'MSFT_StorageReliabilityCounter' -ErrorAction Stop | ForEach-Object {
            Write-Line ('DeviceId=' + $_.DeviceId + ' Temperature=' + $_.Temperature + ' Max=' + $_.TemperatureMax + ' PowerOnHours=' + $_.PowerOnHours)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'WMI PERF PHYSICALDISK'
    try {
        Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk -ErrorAction Stop | ForEach-Object {
            Write-Line ('Name=' + $_.Name + ' PercentDiskTime=' + $_.PercentDiskTime + ' PercentIdleTime=' + $_.PercentIdleTime + ' ReadBytesPerSec=' + $_.DiskReadBytesPerSec + ' WriteBytesPerSec=' + $_.DiskWriteBytesPerSec)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'DOTNET PERFORMANCE COUNTERS'
    try {
        $category = 'PhysicalDisk'
        if ([System.Diagnostics.PerformanceCounterCategory]::Exists($category)) {
            $instances = [System.Diagnostics.PerformanceCounterCategory]::GetInstanceNames($category)
            Write-Line ('Instances: ' + ($instances -join ' | '))
            foreach ($inst in $instances) {
                try {
                    $counter = New-Object System.Diagnostics.PerformanceCounter($category, '% Disk Time', $inst, $true)
                    $null = $counter.NextValue()
                    Start-Sleep -Milliseconds 800
                    $value = $counter.NextValue()
                    Write-Line ('Instance=' + $inst + ' PercentDiskTime=' + [math]::Round($value, 1))
                    $counter.Dispose()
                } catch {
                    Write-Line ('Instance=' + $inst + ' ERROR ' + $_.Exception.Message)
                }
            }
        } else {
            Write-Line ('Category not found: ' + $category)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }

    Write-Section 'NATIVE IOCTL'
    try {
        Add-Type -TypeDefinition $cs -ErrorAction Stop
        $disks = @(Get-CimInstance Win32_DiskDrive -ErrorAction Stop)
        foreach ($d in $disks) {
            $idx = [int]$d.Index
            Write-Line ('Drive=' + $idx + ' TEMP ' + [DiskProbe]::Temp($idx))
            $p1 = [DiskProbe]::Perf($idx)
            Start-Sleep -Milliseconds 1000
            $p2 = [DiskProbe]::Perf($idx)
            Write-Line ('Drive=' + $idx + ' PERF1 ' + $p1)
            Write-Line ('Drive=' + $idx + ' PERF2 ' + $p2)
        }
    } catch {
        Write-Line ('ERROR: ' + $_.Exception.Message)
    }
} finally {
    try {
        $script:Lines | Set-Content -Path $resultPath -Encoding UTF8
        Write-Host ''
        Write-Host ('RESULT_FILE=' + $resultPath) -ForegroundColor Green
    } catch {
        Write-Host ('Failed to save result: ' + $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Host ''
Read-Host 'Probe finished. Press Enter to close'
