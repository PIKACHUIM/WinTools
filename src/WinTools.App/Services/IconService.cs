using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTools.App.Native;

namespace WinTools.App.Services;

/// <summary>
/// 程序图标服务：支持 exe / dll / lnk（系统外壳图标）、ico / png / jpg（直接解码）、
/// 「路径,索引」形式的 DisplayIcon、带引号或带参数的命令行。结果冻结后可跨线程使用，
/// 并按路径做 LRU 缓存（含失败结果），避免滚动列表时反复读盘。
/// </summary>
public sealed partial class IconService
{
    private const int Capacity = 600;
    private const int Concurrency = 4;

    private readonly Dictionary<string, LinkedListNode<Entry>> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<Entry> _order = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _throttle = new(Concurrency);

    private sealed record Entry(string Key, ImageSource? Image);

    /// <summary>异步取图标；找不到时返回 null（界面显示首字母方块）。</summary>
    public async Task<ImageSource?> LoadAsync(string? source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var key = source.Trim();
        if (TryGet(key, out var cached))
        {
            return cached;
        }

        await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGet(key, out cached))
            {
                return cached;
            }

            var image = await Task.Run(() => Load(key), cancellationToken).ConfigureAwait(false);
            Put(key, image);
            return image;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private bool TryGet(string key, out ImageSource? image)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                image = node.Value.Image;
                return true;
            }
        }

        image = null;
        return false;
    }

    private void Put(string key, ImageSource? image)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }

            var node = _order.AddFirst(new Entry(key, image));
            _map[key] = node;

            while (_map.Count > Capacity && _order.Last is { } last)
            {
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }

    private static ImageSource? Load(string source)
    {
        try
        {
            var (path, index) = Parse(source);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => LoadBitmap(path),
                ".ico" => LoadIco(path),
                _ => LoadShellIcon(path, index)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把各种图标来源写法统一成「文件路径 + 图标索引」：
    /// "C:\a.exe,0"、"\"C:\a b\x.exe\" /s"、"C:\a.exe /arg"、%SystemRoot%\x.dll,-100 等。
    /// </summary>
    internal static (string? Path, int Index) Parse(string raw)
    {
        var text = Environment.ExpandEnvironmentVariables(raw.Trim());
        if (text.Length == 0)
        {
            return (null, 0);
        }

        var index = 0;

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            if (end < 0)
            {
                return (null, 0);
            }

            var quoted = text[1..end];
            var rest = text[(end + 1)..].Trim();
            if (rest.StartsWith(',') && int.TryParse(rest[1..].Trim(), out var parsed))
            {
                index = parsed;
            }

            return (quoted, index);
        }

        var comma = text.LastIndexOf(',');
        if (comma > 0 && int.TryParse(text[(comma + 1)..].Trim(), out var idx) && File.Exists(text[..comma]))
        {
            return (text[..comma], idx);
        }

        if (File.Exists(text))
        {
            return (text, 0);
        }

        var match = CommandPathPattern().Match(text);
        return (match.Success ? match.Groups["p"].Value : null, 0);
    }

    [GeneratedRegex(@"^(?<p>.+?\.(exe|dll|ico|png|lnk|msi|cpl|scr))(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CommandPathPattern();

    private static ImageSource? LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        image.DecodePixelWidth = 48;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource? LoadIco(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        // 选最接近 32 像素且不小于它的那一帧；都小于就取最大的
        var frame = decoder.Frames
            .OrderBy(f => f.PixelWidth < 32 ? int.MaxValue - f.PixelWidth : f.PixelWidth - 32)
            .First();

        frame.Freeze();
        return frame;
    }

    private static ImageSource? LoadShellIcon(string path, int index)
    {
        var handle = IntPtr.Zero;

        if (index != 0 && Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".dll" or ".cpl" or ".scr")
        {
            var large = new IntPtr[1];
            if (IconNative.ExtractIconEx(path, index, large, null, 1) > 0)
            {
                handle = large[0];
            }
        }

        if (handle == IntPtr.Zero)
        {
            var info = new IconNative.SHFILEINFO();
            IconNative.SHGetFileInfo(
                path,
                0,
                ref info,
                (uint)Marshal.SizeOf<IconNative.SHFILEINFO>(),
                IconNative.SHGFI_ICON | IconNative.SHGFI_LARGEICON);
            handle = info.hIcon;
        }

        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            IconNative.DestroyIcon(handle);
        }
    }
}
