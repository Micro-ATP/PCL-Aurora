using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

/// <summary>
/// 读取 macOS 的物理总内存和当前可用页数；读取失败时不抛出到启动链路。
/// </summary>
public sealed class MacOSSystemMemoryInfo : ISystemMemoryInfo
{
    private const string SysctlTool = "/usr/sbin/sysctl";
    private const string VmStatTool = "/usr/bin/vm_stat";

    public SystemMemoryInformation Get()
    {
        var fallback = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var totalBytes = TryReadPositiveInt64(RunCommand(SysctlTool, "-n", "hw.memsize"))
            ?? (fallback > 0 ? fallback : null);
        var availableBytes = ParseVmStatAvailableBytes(RunCommand(VmStatTool))
            ?? (fallback > 0 ? fallback : null);
        if (totalBytes is { } total && availableBytes is { } available)
        {
            availableBytes = Math.Min(total, available);
        }

        return new(totalBytes, availableBytes);
    }

    private static long? ParseVmStatAvailableBytes(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var pageSizeMatch = Regex.Match(output, @"page size of (?<size>\d+) bytes", RegexOptions.CultureInvariant);
        if (!pageSizeMatch.Success || !long.TryParse(pageSizeMatch.Groups["size"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pageSize))
        {
            return null;
        }

        long pageCount = 0;
        foreach (var pageType in new[] { "free", "inactive", "speculative" })
        {
            var match = Regex.Match(
                output,
                $@"Pages {pageType}:\s+(?<count>\d+)\.",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success ||
                !long.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                continue;
            }

            checked
            {
                pageCount += count;
            }
        }

        return pageCount > 0 ? checked(pageCount * pageSize) : null;
    }

    private static long? TryReadPositiveInt64(string? output) =>
        long.TryParse(output?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

    private static string? RunCommand(string fileName, params string[] arguments)
    {
        try
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

            if (!process.Start())
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
