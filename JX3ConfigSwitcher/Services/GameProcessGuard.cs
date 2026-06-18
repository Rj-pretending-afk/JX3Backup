using System;
using System.Diagnostics;
using System.Linq;

namespace JX3ConfigSwitcher.Services;

public sealed class GameProcessGuard
{
    private readonly Func<bool>? _isRunningOverride;

    private static readonly string[] ProcessNames =
    {
        "JX3Client",
        "JX3ClientX64",
        "jx3client",
        "jx3clientx64"
    };

    public GameProcessGuard(Func<bool>? isRunningOverride = null)
    {
        _isRunningOverride = isRunningOverride;
    }

    public bool IsGameRunning()
    {
        if (_isRunningOverride is not null)
        {
            return _isRunningOverride();
        }

        return Process.GetProcesses()
            .Any(process =>
            {
                try
                {
                    return ProcessNames.Any(name => process.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            });
    }
}
