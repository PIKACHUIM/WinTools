using System.Runtime.InteropServices;

namespace WinTools.Infrastructure.Backup;

/// <summary>WIMGAPI 常量与 P/Invoke 声明（wimgapi.dll 随系统提供）。</summary>
internal static class WimgApi
{
    private const string Dll = "wimgapi.dll";

    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;

    internal const uint CreateNew = 0;
    internal const uint OpenAlways = 4;
    internal const uint OpenExisting = 3;

    internal const uint CompressNone = 0;
    internal const uint CompressXpress = 1;
    internal const uint CompressLzx = 2;
    internal const uint CompressLzms = 3;

    internal const uint FlagVerify = 0x00000002;
    internal const uint FlagIndex = 0x00000010;
    internal const uint FlagNoFileVerify = 0x00000100;

    // WIM_MSG = WM_USER + 147
    private const uint WimMsg = 0x0400 + 147;
    internal const uint MsgPercent = WimMsg + 3;
    internal const uint MsgProgress = WimMsg + 2;
    internal const uint MsgError = WimMsg + 6;
    internal const uint MsgInfo = WimMsg + 12;

    internal const uint ErrorSuccess = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        public uint MessageId;
        public IntPtr WParam;
        public IntPtr LParam;
    }

    [DllImport(Dll, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr WIMCreateFile(
        string imagePath,
        uint desiredAccess,
        uint creationDisposition,
        uint flagsAndAttributes,
        uint compressionType,
        out uint creationResult);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMCloseHandle(IntPtr handle);

    [DllImport(Dll, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int WIMCaptureImage(IntPtr wimHandle, string path, uint captureFlags);

    [DllImport(Dll, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMApplyImage(IntPtr handle, string path, uint applyFlags);

    [DllImport(Dll, SetLastError = true)]
    internal static extern IntPtr WIMLoadImage(IntPtr wimHandle, uint imageIndex);

    [DllImport(Dll, SetLastError = true)]
    internal static extern uint WIMGetImageCount(IntPtr wimHandle);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMGetImageInformation(IntPtr handle, out IntPtr info, out uint size);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMSetImageInformation(IntPtr handle, byte[] info, uint size);

    internal delegate uint MessageCallback(uint messageId, IntPtr wParam, IntPtr lParam, IntPtr userData);

    [DllImport(Dll, SetLastError = true)]
    internal static extern uint WIMRegisterMessageCallback(
        IntPtr handle,
        MessageCallback callback,
        IntPtr userData);

    [DllImport(Dll, SetLastError = true)]
    internal static extern uint WIMUnregisterMessageCallback(IntPtr handle, MessageCallback callback);

    [DllImport(Dll, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMSetTemporaryPath(IntPtr handle, string path);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WIMDeleteImage(IntPtr wimHandle, uint imageIndex);

    internal static string LastError()
    {
        var code = (uint)Marshal.GetLastWin32Error();
        var description = new global::System.ComponentModel.Win32Exception((int)code).Message;
        return $"Win32 错误 {code}: {description}";
    }
}
