using System.Security.Principal;
using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Infrastructure.Native;

namespace WinTools.Infrastructure.System;

public sealed class SystemInfoService
{
    public SystemSnapshot GetSnapshot()
    {
        var (osName, osVersion) = ReadOsInfo();
        var cpu = ReadCpuName();
        var (totalMemory, availableMemory) = ReadMemory();
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                          ?? "C:\\";

        long driveTotal = 0;
        long driveFree = 0;
        try
        {
            var info = new DriveInfo(systemDrive);
            driveTotal = info.TotalSize;
            driveFree = info.AvailableFreeSpace;
        }
        catch
        {
            // 忽略
        }

        return new SystemSnapshot(
            osName,
            osVersion,
            cpu,
            Environment.ProcessorCount,
            totalMemory,
            availableMemory,
            systemDrive,
            driveTotal,
            driveFree,
            IsElevated());
    }

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static (string Name, string Version) ReadOsInfo()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            if (key is null)
            {
                return (Environment.OSVersion.VersionString, Environment.OSVersion.Version.ToString());
            }

            var productName = key.GetValue("ProductName") as string ?? "Windows";
            var displayVersion = key.GetValue("DisplayVersion") as string
                                 ?? key.GetValue("ReleaseId") as string ?? string.Empty;
            var build = key.GetValue("CurrentBuild") as string ?? Environment.OSVersion.Version.Build.ToString();
            var ubr = key.GetValue("UBR");

            var version = string.IsNullOrEmpty(displayVersion)
                ? $"Build {build}"
                : $"{displayVersion} · Build {build}{(ubr is null ? string.Empty : "." + ubr)}";

            return (productName, version);
        }
        catch
        {
            return (Environment.OSVersion.VersionString, Environment.OSVersion.Version.ToString());
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "CPU";
        }
        catch
        {
            return "CPU";
        }
    }

    private static (long Total, long Available) ReadMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        return ((long)status.ullTotalPhys, (long)status.ullAvailPhys);
    }
}
