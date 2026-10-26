using System.Text;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Utils;

namespace WinTools.App.ViewModels;

/// <summary>系统概览的复制能力：单条复制、单卡复制与整页复制。</summary>
public sealed partial class DashboardViewModel
{
    /// <summary>复制单条硬件信息；参数可以是行对象，也可以直接是文本。</summary>
    [RelayCommand]
    private void CopyLine(object? item)
    {
        var text = item switch
        {
            HardwareLine line => Join(line.Primary, line.Secondary),
            SystemLine system => Join(system.Value, system.Detail),
            HardwareChip chip => $"{chip.Value} ({chip.Label})",
            VolumeRow volume => Join(volume.Label, volume.SizeText, volume.PercentText),
            NetworkLine network => Join(network.Name, network.Status, network.Detail, network.Mac),
            string value => value,
            null => null,
            _ => item.ToString()
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            UI.Clipboard.Copy(text);
        }
    }

    /// <summary>复制某一张卡片的内容；参数是卡片标识（device/cpu/memory/graphics/storage/network/sound）。</summary>
    [RelayCommand]
    private void CopyCard(string? card)
    {
        if (Profile is not { } profile || string.IsNullOrWhiteSpace(card))
        {
            return;
        }

        var sb = new StringBuilder();
        switch (card)
        {
            case "device":
                AppendTitle(sb, DeviceSection, Join(Oem?.DisplayName, ModelText));
                foreach (var line in SystemLines)
                {
                    AppendRow(sb, line.Label, line.Value, line.Detail);
                }

                break;

            case "cpu":
                AppendTitle(sb, CpuLabel, CpuText);
                foreach (var chip in CpuChips)
                {
                    AppendRow(sb, chip.Label, chip.Value, null);
                }

                sb.AppendLine($"{LiveUsageLabel}: {CpuPercent:0}%");
                break;

            case "memory":
                AppendTitle(sb, MemoryLabel, MemorySummary);
                AppendRow(sb, LiveUsageLabel, $"{MemoryPercent:0}%", MemoryText);
                foreach (var chip in MemoryChips)
                {
                    AppendRow(sb, chip.Label, chip.Value, null);
                }

                foreach (var module in MemoryModules)
                {
                    sb.AppendLine($"  · {Join(module.Primary, module.Secondary)}");
                }

                break;

            case "graphics":
                AppendTitle(sb, GraphicsLabel, GpuSummary);
                sb.AppendLine($"{GpuLabel}: {GpuPercent:0}%");
                foreach (var gpu in Graphics)
                {
                    sb.AppendLine($"  · {Join(gpu.Primary, gpu.Secondary)}");
                }

                break;

            case "storage":
                AppendTitle(sb, StorageLabel, DiskSummary);
                foreach (var volume in Volumes)
                {
                    sb.AppendLine($"  · {volume.Label}  {volume.SizeText}  {volume.PercentText}");
                }

                sb.AppendLine($"{PhysicalDisksLabel}:");
                foreach (var disk in Disks)
                {
                    sb.AppendLine($"  · {Join(disk.Primary, disk.Secondary)}");
                }

                break;

            case "network":
                AppendTitle(sb, NetworkSection, NetSummary);
                sb.AppendLine($"{DownloadLabel}: {FormatRate(NetDown)}");
                sb.AppendLine($"{UploadLabel}: {FormatRate(NetUp)}");
                foreach (var network in Networks)
                {
                    sb.AppendLine($"  · {Join(network.Name, network.Status, network.Detail, network.Mac)}");
                }

                break;

            case "sound":
                AppendTitle(sb, SoundLabel, null);
                foreach (var device in SoundDevices)
                {
                    sb.AppendLine($"  · {Join(device.Primary, device.Secondary)}");
                }

                break;

            default:
                return;
        }

        UI.Clipboard.Copy(sb.ToString().TrimEnd());
    }

    /// <summary>把整页硬件信息导出成规整文本，便于贴到工单或群里。</summary>
    [RelayCommand]
    private void CopyAll()
    {
        if (Profile is not { } profile)
        {
            return;
        }

        var sb = new StringBuilder();
        AppendTitle(sb, Loc.T("Dashboard.Title"), Join(profile.ComputerName, ModelText));

        foreach (var line in SystemLines)
        {
            AppendRow(sb, line.Label, line.Value, line.Detail);
        }

        AppendRow(sb, CpuLabel, CpuText, CpuDetailText);
        AppendRow(sb, MemoryLabel, MemorySummary, $"{MemoryPercent:0}%  ·  {MemoryText}");

        sb.AppendLine($"{GraphicsLabel}:");
        foreach (var gpu in Graphics)
        {
            sb.AppendLine($"  · {Join(gpu.Primary, gpu.Secondary)}");
        }

        sb.AppendLine($"{StorageLabel}: {DiskSummary}");
        foreach (var volume in Volumes)
        {
            sb.AppendLine($"  · {volume.Label}  {volume.SizeText}  {volume.PercentText}");
        }

        foreach (var disk in Disks)
        {
            sb.AppendLine($"  · {Join(disk.Primary, disk.Secondary)}");
        }

        sb.AppendLine($"{SoundLabel}: {SoundSummary}");
        sb.AppendLine($"{NetworkSection}: {NetSummary}");
        foreach (var network in Networks)
        {
            sb.AppendLine($"  · {Join(network.Name, network.Status, network.Detail, network.Mac)}");
        }

        sb.AppendLine($"CPU {CpuPercent:0}%  ·  GPU {GpuPercent:0}%  ·  ↓ {FormatRate(NetDown)}  ↑ {FormatRate(NetUp)}");
        sb.AppendLine($"{VersionLabel}: {profile.OsBuild}");

        UI.Clipboard.Copy(sb.ToString().TrimEnd());
    }

    private static string FormatRate(double bytesPerSecond) => SizeFormatter.Format((long)bytesPerSecond) + "/s";

    private static void AppendTitle(StringBuilder sb, string title, string? summary)
        => sb.AppendLine(string.IsNullOrWhiteSpace(summary) ? $"【{title}】" : $"【{title}】{summary}");

    private static void AppendRow(StringBuilder sb, string label, string? primary, string? secondary)
        => sb.AppendLine($"{label}: {Join(primary, secondary)}");
}
