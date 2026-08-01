using System.Diagnostics;
using System.Runtime;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSSystemMemoryOptimizer(ISystemMemoryInfo systemMemoryInfo) : ISystemMemoryOptimizer
{
    private const string PurgeTool = "/usr/sbin/purge";
    private const string OsaScriptTool = "/usr/bin/osascript";

    public async Task<SystemMemoryOptimizationResult> OptimizeAsync(CancellationToken cancellationToken = default)
    {
        var availableBefore = systemMemoryInfo.Get().AvailableBytes;
        var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
        var managedReleased = Math.Max(0, managedBefore - managedAfter);

        if (!File.Exists(PurgeTool))
        {
            return new(false, availableBefore, systemMemoryInfo.Get().AvailableBytes, managedReleased, false,
                "当前 macOS 未提供系统 purge 工具；仅完成了 Aurora 托管内存回收。");
        }

        var direct = await RunAsync(PurgeTool, [], cancellationToken).ConfigureAwait(false);
        var usedAdministratorPrivileges = false;
        ProcessResult result = direct;
        if (direct.ExitCode != 0)
        {
            usedAdministratorPrivileges = true;
            result = await RunAsync(
                OsaScriptTool,
                ["-e", "do shell script \"/usr/sbin/purge\" with administrator privileges"],
                cancellationToken).ConfigureAwait(false);
        }

        var availableAfter = systemMemoryInfo.Get().AvailableBytes;
        return result.ExitCode == 0
            ? new(true, availableBefore, availableAfter, managedReleased, usedAdministratorPrivileges,
                "已清理整个 macOS 系统的文件缓存，并附带回收 Aurora 托管内存。")
            : new(false, availableBefore, availableAfter, managedReleased, usedAdministratorPrivileges,
                string.IsNullOrWhiteSpace(result.Error)
                    ? "系统内存整理未完成。"
                    : $"系统内存整理未完成：{result.Error.Trim()}");
    }

    private static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                return new(-1, "无法启动系统内存整理工具。");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return new(process.ExitCode, string.IsNullOrWhiteSpace(error) ? output : error);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new(-1, exception.Message);
        }
    }

    private sealed record ProcessResult(int ExitCode, string Error);
}
