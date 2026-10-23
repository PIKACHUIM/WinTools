using System.Diagnostics;
using System.Text.Json;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Rules;

namespace WinTools.Infrastructure.Tweaks;

public sealed class TweakService : ITweakService
{
    private readonly RuleRepository _repository;
    private readonly IAppLogger _logger;

    public TweakService(RuleRepository repository, IAppLogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<TweakGroup> Groups => _repository.TweakGroups;

    public IReadOnlyList<TweakDefinition> Tweaks => _repository.Tweaks;

    public Task<IReadOnlyList<TweakState>> DetectAsync(CancellationToken cancellationToken)
        => Task.Run<IReadOnlyList<TweakState>>(() =>
        {
            var states = new List<TweakState>(_repository.Tweaks.Count);

            foreach (var tweak in _repository.Tweaks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    states.Add(new TweakState
                    {
                        Definition = tweak,
                        IsApplied = IsApplied(tweak)
                    });
                }
                catch (Exception ex)
                {
                    states.Add(new TweakState { Definition = tweak, IsApplied = false, Error = ex.Message });
                }
            }

            return states;
        }, cancellationToken);

    public Task<TweakApplyReport> ApplyAsync(IReadOnlyCollection<string> tweakIds, CancellationToken cancellationToken)
        => Task.Run(() => Run(tweakIds, apply: true, cancellationToken), cancellationToken);

    public Task<TweakApplyReport> RevertAsync(IReadOnlyCollection<string> tweakIds, CancellationToken cancellationToken)
        => Task.Run(() => Run(tweakIds, apply: false, cancellationToken), cancellationToken);

    private TweakApplyReport Run(IReadOnlyCollection<string> ids, bool apply, CancellationToken cancellationToken)
    {
        var report = new TweakApplyReport();
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

        foreach (var tweak in _repository.Tweaks.Where(t => set.Contains(t.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operations = apply ? tweak.Apply : tweak.Revert;
            try
            {
                foreach (var op in operations)
                {
                    ExecuteOperation(op);
                }

                if (apply)
                {
                    report.Applied++;
                }
                else
                {
                    report.Reverted++;
                }

                if (tweak.RequiresRestart)
                {
                    report.RestartRequired = true;
                }

                _logger.Audit(apply ? "tweak.apply" : "tweak.revert", tweak.Id,
                    tweak.Name.Resolve(string.Empty));
            }
            catch (Exception ex)
            {
                report.Failed++;
                report.Errors.Add($"{tweak.Id}: {ex.Message}");
                _logger.Error("Tweak", $"{(apply ? "应用" : "回滚")}失败 {tweak.Id}", ex);
            }
        }

        return report;
    }

    private static bool IsApplied(TweakDefinition tweak)
    {
        if (tweak.Detect.Count == 0)
        {
            return false;
        }

        foreach (var op in tweak.Detect)
        {
            switch (op.Type)
            {
                case TweakOpKind.RegSet:
                    if (!RegistryOps.ValueMatches(op, out _))
                    {
                        return false;
                    }

                    break;

                case TweakOpKind.ServiceStart:
                {
                    var current = RegistryOps.ReadServiceStartType(op.Service ?? string.Empty);
                    if (current is null || current.Value != RegistryOps.ToStartType(op.StartMode))
                    {
                        return false;
                    }

                    break;
                }

                default:
                    return false;
            }
        }

        return true;
    }

    private void ExecuteOperation(TweakOperation op)
    {
        switch (op.Type)
        {
            case TweakOpKind.RegSet:
                RegistryOps.SetValue(op);
                break;

            case TweakOpKind.RegDeleteValue:
                RegistryOps.DeleteValue(op);
                break;

            case TweakOpKind.RegDeleteKey:
                RegistryOps.DeleteKeyTree(op);
                break;

            case TweakOpKind.ServiceStart:
            {
                var startType = RegistryOps.ToStartType(op.StartMode);
                if (!RegistryOps.ChangeServiceStartType(op.Service ?? string.Empty, startType))
                {
                    throw new InvalidOperationException($"修改服务启动类型失败: {op.Service}");
                }

                break;
            }

            case TweakOpKind.Command:
                RunCommand(op);
                break;

            default:
                throw new NotSupportedException(op.Type.ToString());
        }
    }

    private void RunCommand(TweakOperation op)
    {
        if (string.IsNullOrWhiteSpace(op.FileName))
        {
            return;
        }

        var info = new ProcessStartInfo(op.FileName, op.Arguments ?? string.Empty)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"无法启动进程: {op.FileName}");

        if (!process.WaitForExit(30_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略
            }

            throw new TimeoutException($"命令超时: {op.FileName} {op.Arguments}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"命令返回 {process.ExitCode}: {op.FileName} {op.Arguments}");
        }
    }
}
