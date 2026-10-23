using System.Net.NetworkInformation;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.System;

/// <summary>硬件档案采集：一次 WMI 批量查询，结果缓存由调用方决定。</summary>
public sealed class HardwareInfoService
{
    public HardwareProfile GetProfile()
    {
        var computer = Wmi.Query(
            "SELECT Name, TotalPhysicalMemory, Manufacturer, Model FROM Win32_ComputerSystem");

        var os = Wmi.Query(
            "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem");

        var board = Wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard");
        var bios = Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion FROM Win32_BIOS");
        var cpu = Wmi.Query(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, SocketDesignation, MaxClockSpeed FROM Win32_Processor");

        var graphics = Wmi.Query(
            "SELECT Name, AdapterRAM, DriverVersion, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController");

        var memory = Wmi.Query(
            "SELECT DeviceLocator, BankLabel, Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory");

        var disks = Wmi.Query(
            "SELECT Model, Size, InterfaceType, MediaType, SerialNumber FROM Win32_DiskDrive");

        var sound = Wmi.Query("SELECT Name FROM Win32_SoundDevice");

        var adapters = Wmi.Query(
            "SELECT Name, MACAddress, Speed, AdapterType, NetConnectionStatus, NetEnabled FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE");

        var totalMemory = ToLong(Wmi.First(computer, "TotalPhysicalMemory"));
        var physicalCores = ToInt(Wmi.First(cpu, "NumberOfCores"));
        var logicalCores = ToInt(Wmi.First(cpu, "NumberOfLogicalProcessors"));
        var clock = ToInt(Wmi.First(cpu, "MaxClockSpeed"));

        return new HardwareProfile(
            Text(Wmi.First(computer, "Name"), Environment.MachineName),
            Text(Wmi.First(os, "Caption"), "Windows").Replace("Microsoft ", string.Empty).Trim(),
            Text(Wmi.First(os, "Version"), Environment.OSVersion.Version.ToString()),
            Text(Wmi.First(os, "BuildNumber"), Environment.OSVersion.Version.Build.ToString()),
            Text(Wmi.First(os, "OSArchitecture"), Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
            Text(Wmi.First(board, "Manufacturer"), "—"),
            Text(Wmi.First(board, "Product"), "—"),
            Wmi.FirstString(bios, "Manufacturer"),
            Wmi.FirstString(bios, "SMBIOSBIOSVersion"),
            Text(Wmi.First(cpu, "Name")),
            physicalCores > 0 ? physicalCores : logicalCores,
            logicalCores > 0 ? logicalCores : Environment.ProcessorCount,
            Wmi.FirstString(cpu, "SocketDesignation"),
            clock > 0 ? $"{clock / 1000d:0.0} GHz" : null,
            ReadGraphics(graphics),
            totalMemory,
            ReadMemoryModules(memory),
            ReadDisks(disks),
            sound.Select(r => Text(r.TryGetValue("Name", out var n) ? n : null))
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList(),
            ReadAdapters(adapters),
            ReadVolumes());
    }

    private static List<GraphicsAdapterInfo> ReadGraphics(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var list = new List<GraphicsAdapterInfo>();

        foreach (var row in rows)
        {
            var name = Text(row.TryGetValue("Name", out var n) ? n : null);
            if (name.Length == 0)
            {
                continue;
            }

            var vram = ToLong(row.TryGetValue("AdapterRAM", out var v) ? v : null);
            var width = ToInt(row.TryGetValue("CurrentHorizontalResolution", out var w) ? w : null);
            var height = ToInt(row.TryGetValue("CurrentVerticalResolution", out var h) ? h : null);

            list.Add(new GraphicsAdapterInfo(
                name,
                vram > 0 ? SizeFormatter.Format(vram) : null,
                Text(row.TryGetValue("DriverVersion", out var d) ? d : null) is { Length: > 0 } driver ? driver : null,
                width > 0 && height > 0 ? $"{width} × {height}" : null));
        }

        return list;
    }

    private static List<MemoryModuleInfo> ReadMemoryModules(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var list = new List<MemoryModuleInfo>();
        var index = 1;

        foreach (var row in rows)
        {
            var capacity = ToLong(row.TryGetValue("Capacity", out var c) ? c : null);
            if (capacity <= 0)
            {
                continue;
            }

            var slot = Text(row.TryGetValue("DeviceLocator", out var d) ? d : null);
            if (slot.Length == 0)
            {
                slot = Text(row.TryGetValue("BankLabel", out var b) ? b : null);
            }

            list.Add(new MemoryModuleInfo(
                slot.Length == 0 ? $"DIMM {index}" : slot,
                capacity,
                ToInt(row.TryGetValue("Speed", out var s) ? s : null) is var speed && speed > 0
                    ? $"{speed} MHz"
                    : null,
                Text(row.TryGetValue("Manufacturer", out var m) ? m : null) is { Length: > 0 } manu ? manu : null,
                Text(row.TryGetValue("PartNumber", out var p) ? p : null) is { Length: > 0 } part ? part : null));

            index++;
        }

        return list;
    }

    private static List<PhysicalDiskInfo> ReadDisks(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var list = new List<PhysicalDiskInfo>();

        foreach (var row in rows)
        {
            var size = ToLong(row.TryGetValue("Size", out var s) ? s : null);
            if (size <= 0)
            {
                continue;
            }

            list.Add(new PhysicalDiskInfo(
                Text(row.TryGetValue("Model", out var m) ? m : null),
                size,
                Text(row.TryGetValue("InterfaceType", out var i) ? i : null) is { Length: > 0 } itf ? itf : null,
                Text(row.TryGetValue("MediaType", out var md) ? md : null) is { Length: > 0 } media ? media : null,
                Text(row.TryGetValue("SerialNumber", out var sn) ? sn : null) is { Length: > 0 } serial ? serial.Trim() : null));
        }

        return list;
    }

    private static List<NetworkAdapterInfo> ReadAdapters(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var list = new List<NetworkAdapterInfo>();
        var addresses = ReadIpv4Map();

        foreach (var row in rows)
        {
            var name = Text(row.TryGetValue("Name", out var n) ? n : null);
            if (name.Length == 0)
            {
                continue;
            }

            var mac = Text(row.TryGetValue("MACAddress", out var m) ? m : null);
            var status = ToInt(row.TryGetValue("NetConnectionStatus", out var st) ? st : null);
            var speed = ToLong(row.TryGetValue("Speed", out var sp) ? sp : null);

            list.Add(new NetworkAdapterInfo(
                name,
                mac.Length > 0 ? mac : null,
                speed > 0 ? $"{speed / 1_000_000d:0} Mbps" : null,
                Text(row.TryGetValue("AdapterType", out var t) ? t : null) is { Length: > 0 } type ? type : null,
                status == 2,
                mac.Length > 0 && addresses.TryGetValue(NormalizeMac(mac), out var ip) ? ip : null));
        }

        return list;
    }

    private static Dictionary<string, string> ReadIpv4Map()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var address = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == global::System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString();

                if (address is not null && !string.IsNullOrEmpty(nic.GetPhysicalAddress().ToString()))
                {
                    map[NormalizeMac(nic.GetPhysicalAddress().ToString())] = address;
                }
            }
        }
        catch
        {
            // 忽略
        }

        return map;
    }

    private static string NormalizeMac(string mac)
        => mac.Replace(":", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    /// <summary>只取卷容量（DriveInfo），用于需要快速展示磁盘占用的页面。</summary>
    public IReadOnlyList<VolumeInfo> GetVolumes() => ReadVolumes();

    private static List<VolumeInfo> ReadVolumes()
    {
        var list = new List<VolumeInfo>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                try
                {
                    list.Add(new VolumeInfo(
                        drive.Name,
                        string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name.TrimEnd('\\') : drive.VolumeLabel,
                        drive.DriveFormat,
                        drive.TotalSize,
                        drive.AvailableFreeSpace,
                        drive.IsReady));
                }
                catch
                {
                    // 单个卷读取失败忽略
                }
            }
        }
        catch
        {
            // 忽略
        }

        return list;
    }

    private static string Text(object? value, string fallback = "")
    {
        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static long ToLong(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt64(value);
        }
        catch
        {
            return 0;
        }
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }
}
