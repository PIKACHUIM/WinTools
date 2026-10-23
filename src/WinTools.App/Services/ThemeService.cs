using System.Windows;
using WinTools.Core.Models;

namespace WinTools.App.Services;

/// <summary>在 Light.xaml / Dark.xaml 之间热切换（只替换第一个字典，其余样式保持引用）。</summary>
public sealed class ThemeService
{
    private const string LightSource = "Themes/Light.xaml";
    private const string DarkSource = "Themes/Dark.xaml";

    public AppTheme Current { get; private set; } = AppTheme.System;

    public void Apply(AppTheme theme)
    {
        Current = theme;

        var effective = theme switch
        {
            AppTheme.Light => AppTheme.Light,
            AppTheme.Dark => AppTheme.Dark,
            _ => IsSystemDark() ? AppTheme.Dark : AppTheme.Light
        };

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var target = new ResourceDictionary
        {
            Source = new Uri(effective == AppTheme.Dark ? DarkSource : LightSource, UriKind.Relative)
        };

        var index = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            dictionaries[index] = target;
        }
        else
        {
            dictionaries.Insert(0, target);
        }

    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return true;
        }
    }
}
