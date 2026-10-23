using System.Runtime.InteropServices;
using System.Windows;

namespace WinTools.App.Services;

/// <summary>剪贴板：处理被其他程序短暂占用时的重试（CLIPBRD_E_CANT_OPEN）。</summary>
public sealed class ClipboardService
{
    private readonly ToastService _toast;

    public ClipboardService(ToastService toast) => _toast = toast;

    /// <summary>复制文本，成功后提示「已复制」。</summary>
    public bool Copy(string? text, bool notify = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                if (notify)
                {
                    _toast.Success(Localization.Loc.T("Common.Copied"));
                }

                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
            catch (ExternalException)
            {
                Thread.Sleep(40);
            }
        }

        _toast.Error(Localization.Loc.T("Common.Failed"));
        return false;
    }
}
