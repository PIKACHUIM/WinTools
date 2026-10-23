using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.System;

/// <summary>
/// 实时占用采样：CPU / 内存 / 磁盘活动 / 网络吞吐 / GPU。
/// 首次调用返回 0（缺少上一次采样的差值），之后按两次采样的间隔计算速率。
/// </summary>
public sealed class SystemMetricsService
{
    private readonly object _gate = new();
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private long _prevTimestamp;

    public SystemMetricsSample Sample()
    {
        lock (_gate)
        {
            var now = Environment.TickCount64;
            var (cpu, _) = ReadCpu(now);
            var (memoryUsed, memoryTotal, memoryPercent) = ReadMemory();
            var (down, up) = ReadNetwork(now);

            return new SystemMetricsSample(
                cpu,
                memoryPercent,
                memoryUsed,
                memoryTotal,
                ReadDiskActivity(),
                down,
                up,
                ReadGpu(),
                now / 1000);
        }
    }

    private (double Percent, long Timestamp) ReadCpu(long now)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return (0, now);
        }

        var idleTicks = ToUInt64(idle);
        var kernelTicks = ToUInt64(kernel);
        var userTicks = ToUInt64(user);

        if (_prevKernel == 0 && _prevUser == 0)
        {
            _prevIdle = idleTicks;
            _prevKernel = kernelTicks;
            _prevUser = userTicks;
            return (0, now);
        }

        var idleDelta = idleTicks - _prevIdle;
        var totalDelta = (kernelTicks - _prevKernel) + (userTicks - _prevUser);

        _prevIdle = idleTicks;
        _prevKernel = kernelTicks;
        _prevUser = userTicks;

        if (totalDelta == 0)
        {
            return (0, now);
        }

        var busy = 100d * (totalDelta - idleDelta) / totalDelta;
        return (Math.Clamp(busy, 0, 100), now);
    }

    private static (long Used, long Total, double Percent) ReadMemory()
    {
        var status = new MemoryStatus
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0, 0);
        }

        var total = (long)status.TotalPhysical;
        var used = total - (long)status.AvailablePhysical;

        return (used, total, total <= 0 ? 0 : Math.Clamp(used * 100d / total, 0, 100));
    }

    private (double Down, double Up) ReadNetwork(long now)
    {
        long received = 0;
        long sent = 0;

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var stats = nic.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
        }
        catch
        {
            return (0, 0);
        }

        if (_prevTimestamp == 0)
        {
            _prevBytesReceived = received;
            _prevBytesSent = sent;
            _prevTimestamp = now;
            return (0, 0);
        }

        var elapsed = (now - _prevTimestamp) / 1000d;
        if (elapsed <= 0.05)
        {
            return (0, 0);
        }

        var down = Math.Max(0, received - _prevBytesReceived) / elapsed;
        var up = Math.Max(0, sent - _prevBytesSent) / elapsed;

        _prevBytesReceived = received;
        _prevBytesSent = sent;
        _prevTimestamp = now;

        return (down, up);
    }

    private static double ReadDiskActivity()
    {
        var rows = Wmi.Query(
            "SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name='_Total'",
            @"root\cimv2");

        var value = Wmi.First(rows, "PercentDiskTime");
        return value is null ? 0 : Math.Clamp(Convert.ToDouble(value), 0, 100);
    }

    private static double ReadGpu()
    {
        var rows = Wmi.Query(
            "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine",
            @"root\cimv2");

        if (rows.Count == 0)
        {
            return 0;
        }

        double total = 0;
        foreach (var row in rows)
        {
            var name = row.TryGetValue("Name", out var n) ? n?.ToString() : null;
            if (name is null || !name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (row.TryGetValue("UtilizationPercentage", out var value) && value is not null)
            {
                total += Convert.ToDouble(value);
            }
        }

        return Math.Clamp(total, 0, 100);
    }

    private static ulong ToUInt64(global::System.Runtime.InteropServices.ComTypes.FILETIME time)
        => ((ulong)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out global::System.Runtime.InteropServices.ComTypes.FILETIME idleTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME kernelTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME userTime);
}
