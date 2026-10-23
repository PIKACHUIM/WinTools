using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>功能开关的导入 / 导出 / 重置。</summary>
public sealed partial class FeatureFlagsSectionViewModel
{
    private const string ExportFilter = "JSON (*.json)|*.json|所有文件 (*.*)|*.*";

    protected override void OnLoaded()
    {
        base.OnLoaded();

        if (ExtraActions.Count == 0)
        {
            ExtraActions.Add(new ToolbarAction(Loc.T("Flag.Export"), ExportCommand));
            ExtraActions.Add(new ToolbarAction(Loc.T("Flag.Import"), ImportCommand));
            ExtraActions.Add(new ToolbarAction(Loc.T("Flag.Reset"), ResetCommand));
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var dialog = new SaveFileDialog
        {
            FileName = "wintools-feature-flags.json",
            Filter = ExportFilter,
            Title = Loc.T("Flag.Export")
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var entries = await _flags.GetOverridesAsync(ct).ConfigureAwait(true);
            var payload = entries.Select(e => new FlagFileEntry(e.FeatureId, e.EnabledState, e.Variant)).ToList();

            await File.WriteAllTextAsync(
                dialog.FileName,
                JsonSerializer.Serialize(payload, FlagJson.Options),
                ct).ConfigureAwait(true);

            UI.Toast.Success(Loc.F("Common.TotalCount", payload.Count));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = ExportFilter,
            Title = Loc.T("Flag.Import"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var text = await File.ReadAllTextAsync(dialog.FileName, ct).ConfigureAwait(true);
            var entries = JsonSerializer.Deserialize<List<FlagFileEntry>>(text, FlagJson.Options) ?? [];

            var ok = 0;
            var failed = 0;

            ShowProgress = true;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                ProgressPercent = (int)(i * 100d / Math.Max(1, entries.Count));

                var result = await _flags.AddAsync(entry.FeatureId, entry.EnabledState, ct).ConfigureAwait(true);
                if (result.Success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                }
            }

            await RefreshQuietlyAsync(ct).ConfigureAwait(true);

            Result = new ResultSummary(
                failed == 0 ? Loc.T("Flag.Imported") : Loc.T("Common.ResultSummary"),
                failed == 0 ? ResultKind.Success : ok > 0 ? ResultKind.Warning : ResultKind.Error,
                [
                    new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                    new ResultMetric(Loc.T("Common.Failed"), failed.ToString())
                ]);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        var entries = Rows.Select(r => r.Tag).OfType<FeatureFlagEntry>().ToList();
        if (entries.Count == 0)
        {
            UI.Toast.Info(Loc.T("Flag.Empty"));
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Flag.Reset"),
            Loc.F("Flag.ResetConfirm", entries.Count),
            entries.Take(20).Select(e => e.FeatureId).ToList(),
            Loc.T("Flag.Reset"),
            Danger: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var ok = 0;
            var failed = 0;
            ShowProgress = true;

            for (var i = 0; i < entries.Count; i++)
            {
                ProgressPercent = (int)(i * 100d / entries.Count);
                var result = await _flags.RemoveAsync(entries[i], ct).ConfigureAwait(true);
                if (result.Success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                }
            }

            await RefreshQuietlyAsync(ct).ConfigureAwait(true);

            Result = new ResultSummary(
                failed == 0 ? Loc.T("Flag.ResetDone") : Loc.T("Common.ResultSummary"),
                failed == 0 ? ResultKind.Success : ResultKind.Warning,
                [
                    new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                    new ResultMetric(Loc.T("Common.Failed"), failed.ToString())
                ]);
        }).ConfigureAwait(true);
    }

    /// <summary>导入文件的条目结构。</summary>
    private sealed record FlagFileEntry(string FeatureId, int EnabledState, int Variant);
}

internal static class FlagJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
