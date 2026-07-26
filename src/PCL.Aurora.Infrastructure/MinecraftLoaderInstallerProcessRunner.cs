using System.Diagnostics;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftLoaderInstallerProcessRunner : IMinecraftLoaderInstallerProcessRunner
{
    private const int MaximumOutputLines = 200;

    public async Task<MinecraftLoaderInstallerExecutionResult> ExecuteAsync(
        MinecraftLoaderInstallerProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(request.JavaExecutablePath)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in request.ArgumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new(null, [], ["无法启动加载器安装进程。"]);
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        var output = new List<GameProcessOutput>();
        var outputLock = new object();
        var standardOutput = ReadOutputAsync(process.StandardOutput, false, output, outputLock);
        var standardError = ReadOutputAsync(process.StandardError, true, output, outputLock);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            return process.ExitCode == 0
                ? new(process.ExitCode, output, [])
                : new(process.ExitCode, output, [$"加载器安装器以退出代码 {process.ExitCode} 结束。"]);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static async Task ReadOutputAsync(
        StreamReader reader,
        bool isError,
        ICollection<GameProcessOutput> output,
        object outputLock)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            lock (outputLock)
            {
                if (output.Count < MaximumOutputLines)
                {
                    output.Add(new(isError, line));
                }
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
