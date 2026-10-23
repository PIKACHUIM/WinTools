namespace WinTools.Core.Models;

/// <summary>显卡（可能有多块）。</summary>
public sealed record GraphicsAdapterInfo(
    string Name,
    string? MemoryText,
    string? DriverVersion,
    string? Resolution);

/// <summary>一根内存条。</summary>
public sealed record MemoryModuleInfo(
    string Slot,
    long CapacityBytes,
    string? Speed,
    string? Manufacturer,
    string? PartNumber);

/// <summary>物理磁盘。</summary>
public sealed record PhysicalDiskInfo(
    string Model,
    long SizeBytes,
    string? InterfaceType,
    string? MediaType,
    string? SerialNumber);

/// <summary>分区 / 卷。</summary>
public sealed record VolumeInfo(
    string Name,
    string Label,
    string DriveFormat,
    long TotalBytes,
    long FreeBytes,
    bool IsReady)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? 0 : UsedBytes * 100d / TotalBytes;
}

/// <summary>网卡。</summary>
public sealed record NetworkAdapterInfo(
    string Name,
    string? MacAddress,
    string? LinkSpeed,
    string? AdapterType,
    bool IsConnected,
    string? Ipv4);

/// <summary>整机硬件档案（启动时采集一次，刷新时重采）。</summary>
public sealed record HardwareProfile(
    string ComputerName,
    string OsName,
    string OsVersion,
    string OsBuild,
    string Architecture,
    string MotherboardVendor,
    string MotherboardProduct,
    string? BiosVendor,
    string? BiosVersion,
    string CpuName,
    int PhysicalCores,
    int LogicalCores,
    string? CpuSocket,
    string? CpuClockText,
    IReadOnlyList<GraphicsAdapterInfo> Graphics,
    long TotalMemoryBytes,
    IReadOnlyList<MemoryModuleInfo> MemoryModules,
    IReadOnlyList<PhysicalDiskInfo> Disks,
    IReadOnlyList<string> SoundDevices,
    IReadOnlyList<NetworkAdapterInfo> NetworkAdapters,
    IReadOnlyList<VolumeInfo> Volumes);

/// <summary>一次实时采样。</summary>
public sealed record SystemMetricsSample(
    double CpuPercent,
    double MemoryPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DiskActivePercent,
    double NetworkDownBytesPerSecond,
    double NetworkUpBytesPerSecond,
    double GpuPercent,
    long UptimeSeconds);
