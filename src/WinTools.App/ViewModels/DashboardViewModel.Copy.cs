using System.Text;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;

namespace WinTools.App.ViewModels;

/// <summary>系统概览的复制能力：单条复制与整页复制。</summary>
public sealed partial class DashboardViewModel
{
    public string CopyHint => Loc.T("Common.Copy");

    public string CopyAllLabel => Loc.T("Common.CopyAll");

    /// <summary>复制单条硬件信息；参数可以是行对象，也可以直接是文本。</summary>
    [RelayCommand]
    private void CopyLine(object? item)
    {
        var text = item switch
        {
            HardwareLine line => Join(line.Primary, line.Secondary),
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

    /// <summary>把整页硬件信息导出成规整文本，便于贴到工单或群里。</summary>
    [RelayCommand]
    private void CopyAll()
    {
        if (Profile is not { } profile)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"【{Loc.T("Dashboard.Title")}】{profile.ComputerName}");
        AppendRow(sb, SystemLabel, OsText, OsDetailText);
        AppendRow(sb, BoardLabel, BoardText, null);
        AppendRow(sb, CpuLabel, CpuText, CpuDetailText);

        sb.AppendLine($"{GraphicsLabel}:");
        foreach (var gpu in Graphics)
        {
            sb.AppendLine($"  · {Join(gpu.Primary, gpu.Secondary)}");
        }

        sb.AppendLine($"{MemoryModuleLabel}: {MemorySummary}");
        foreach (var module in MemoryModules)
        {
            sb.AppendLine($"  · {Join(module.Primary, module.Secondary)}");
        }

        sb.AppendLine($"{DiskLabel2}:");
        foreach (var disk in Disks)
        {
            sb.AppendLine($"  · {Join(disk.Primary, disk.Secondary)}");
        }

        sb.AppendLine($"{VolumeLabel}:");
        foreach (var volume in Volumes)
        {
            sb.AppendLine($"  · {volume.Label}  {volume.SizeText}  {volume.PercentText}");
        }

        sb.AppendLine($"{SoundLabel}:");
        foreach (var device in SoundDevices)
        {
            sb.AppendLine($"  · {Join(device.Primary, device.Secondary)}");
        }

        sb.AppendLine($"{NetworkSection}:");
        foreach (var network in Networks)
        {
            sb.AppendLine($"  · {Join(network.Name, network.Status, network.Detail, network.Mac)}");
        }

        sb.AppendLine($"{VersionLabel}: {profile.OsBuild}");
        sb.AppendLine($"CPU {CpuPercent:0}%  ·  内存 {MemoryPercent:0}%  ·  磁盘 {DiskUsagePercent:0}%  ·  GPU {GpuPercent:0}%");

        UI.Clipboard.Copy(sb.ToString().TrimEnd());
    }

    private static void AppendRow(StringBuilder sb, string label, string? primary, string? secondary)
        => sb.AppendLine($"{label}: {Join(primary, secondary)}");
}
