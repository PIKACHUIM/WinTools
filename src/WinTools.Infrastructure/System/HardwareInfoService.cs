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
            "SELECT Name, TotalPhysicalMemory, Manufacturer, Model, PCSystemType FROM Win32_ComputerSystem");

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
            "SELECT Index, Model, Size, InterfaceType, MediaType, SerialNumber, PNPDeviceID FROM Win32_DiskDrive");

        // Win32_DiskDrive.InterfaceType 对 SATA 磁盘同样返回 IDE（AHCI 走的还是 ATA 命令集），
        // 总线类型以存储命名空间的 MSFT_PhysicalDisk.BusType 为准。
        var storageDisks = Wmi.Query(
            "SELECT DeviceId, SerialNumber, BusType, MediaType FROM MSFT_PhysicalDisk",
            @"root\Microsoft\Windows\Storage");

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
            Text(Wmi.First(computer, "Manufacturer")),
            Text(Wmi.First(computer, "Model")),
            ToInt(Wmi.First(computer, "PCSystemType")),
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
            ReadDisks(disks, storageDisks),
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

    private static List<PhysicalDiskInfo> ReadDisks(
        IReadOnlyList<Dictionary<string, object?>> rows,
        IReadOnlyList<Dictionary<string, object?>> storageRows)
    {
        var byIndex = new Dictionary<int, StorageDisk>();
        var bySerial = new Dictionary<string, StorageDisk>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in storageRows)
        {
            var storage = new StorageDisk(
                ToInt(row.TryGetValue("BusType", out var bt) ? bt : null),
                ToInt(row.TryGetValue("MediaType", out var mt) ? mt : null));

            if (int.TryParse(Text(row.TryGetValue("DeviceId", out var id) ? id : null), out var deviceId))
            {
                byIndex[deviceId] = storage;
            }

            var storageSerial = NormalizeSerial(Text(row.TryGetValue("SerialNumber", out var sn) ? sn : null));
            if (storageSerial.Length > 0)
            {
                bySerial[storageSerial] = storage;
            }
        }

        var list = new List<PhysicalDiskInfo>();

        foreach (var row in rows)
        {
            var size = ToLong(row.TryGetValue("Size", out var s) ? s : null);
            if (size <= 0)
            {
                continue;
            }

            var serial = Text(row.TryGetValue("SerialNumber", out var sn) ? sn : null) is { Length: > 0 } rawSerial
                ? rawSerial.Trim()
                : null;

            StorageDisk? storage = null;
            if (int.TryParse(Text(row.TryGetValue("Index", out var idx) ? idx : null), out var index)
                && byIndex.TryGetValue(index, out var indexed))
            {
                storage = indexed;
            }

            if (storage is null
                && serial is not null
                && bySerial.TryGetValue(NormalizeSerial(serial), out var serialized))
            {
                storage = serialized;
            }

            list.Add(new PhysicalDiskInfo(
                Text(row.TryGetValue("Model", out var m) ? m : null),
                size,
                ResolveBusType(
                    storage?.BusType ?? 0,
                    Text(row.TryGetValue("PNPDeviceID", out var pnp) ? pnp : null),
                    Text(row.TryGetValue("InterfaceType", out var itf) ? itf : null)),
                ResolveMediaType(
                    storage?.MediaType ?? 0,
                    Text(row.TryGetValue("MediaType", out var md) ? md : null)),
                serial));
        }

        return list;
    }

    /// <summary>MSFT_PhysicalDisk 里用到的两个枚举字段（BusType / MediaType）。</summary>
    private sealed record StorageDisk(int BusType, int MediaType);

    private static string? ResolveBusType(int busType, string devicePath, string legacyInterface)
    {
        var hint = BusTypeFromDevicePath(devicePath);

        if (busType > 0)
        {
            // SCSI / ATA / RAID 只说明走了哪个控制器，底层链路可能是 SATA、NVMe 或 IDE，
            // 这类值用设备实例路径修正；其余（SATA、NVMe、USB、SAS…）直接采用。
            return busType is 1 or 2 or 3 or 8 && hint is not null ? hint : BusTypeText(busType);
        }

        return hint ?? (legacyInterface.Length > 0 ? legacyInterface : null);
    }

    /// <summary>BusType 枚举值 → 展示文本（与「任务管理器 / 磁盘管理」口径一致）。</summary>
    private static string? BusTypeText(int busType) => busType switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "IEEE 1394",
        5 => "SSA",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        14 => "Virtual",
        15 => "Virtual",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => null
    };

    /// <summary>
    /// 从设备实例路径反推物理链路：以 IDE\ 开头的才是真正的 PATA，
    /// SCSI 路径里 VEN_ATA 是 AHCI 下的 SATA，VEN_NVME 是 NVMe。
    /// </summary>
    private static string? BusTypeFromDevicePath(string devicePath)
    {
        if (devicePath.Length == 0)
        {
            return null;
        }

        if (devicePath.StartsWith("USBSTOR", StringComparison.OrdinalIgnoreCase))
        {
            return "USB";
        }

        if (devicePath.StartsWith("IDE\\", StringComparison.OrdinalIgnoreCase))
        {
            return "IDE";
        }

        if (!devicePath.StartsWith("SCSI\\", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (devicePath.Contains("VEN_NVME", StringComparison.OrdinalIgnoreCase))
        {
            return "NVMe";
        }

        if (devicePath.Contains("VEN_ATA", StringComparison.OrdinalIgnoreCase)
            || devicePath.Contains("VEN_ATAPI", StringComparison.OrdinalIgnoreCase))
        {
            return "SATA";
        }

        if (VirtualDiskVendors.Any(v => devicePath.Contains(v, StringComparison.OrdinalIgnoreCase)))
        {
            return "Virtual";
        }

        return null;
    }

    private static readonly string[] VirtualDiskVendors =
        ["VEN_MSFT", "VEN_VMWARE", "VEN_VBOX", "VEN_QEMU", "VEN_PRLS", "VEN_XEN"];

    private static string? ResolveMediaType(int mediaType, string legacyMediaType) => mediaType switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => legacyMediaType.Length > 0 ? legacyMediaType : null
    };

    private static string NormalizeSerial(string serial)
        => new string(serial.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

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
