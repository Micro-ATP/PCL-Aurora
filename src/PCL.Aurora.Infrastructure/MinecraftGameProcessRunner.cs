using System.Diagnostics;
using System.Threading.Channels;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftGameProcessRunner : IGameProcessRunner
{
    public async Task<GameProcessSession> StartAsync(
        MinecraftGameLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await RunPreLaunchCommandAsync(request, cancellationToken).ConfigureAwait(false);
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

        foreach (var (key, value) in request.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("无法启动游戏进程。");
        }
        TryApplyPriority(process, request.ProcessPriority);

        var output = Channel.CreateUnbounded<GameProcessOutput>();
        return new GameProcessSession(
            process.Id,
            output.Reader,
            CompleteAsync(process, output.Writer));
    }

    private static async Task RunPreLaunchCommandAsync(
        MinecraftGameLaunchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PreLaunchCommand))
        {
            return;
        }

        var shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var startInfo = new ProcessStartInfo(shell)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        startInfo.ArgumentList.Add(request.PreLaunchCommand);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法执行启动前命令。");
        if (!request.WaitForPreLaunchCommand)
        {
            process.Dispose();
            return;
        }

        using (process)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"启动前命令退出，代码 {process.ExitCode}。");
            }
        }
    }

    private static void TryApplyPriority(Process process, MinecraftGameProcessPriority priority)
    {
        try
        {
            process.PriorityClass = priority switch
            {
                MinecraftGameProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                MinecraftGameProcessPriority.High => ProcessPriorityClass.High,
                MinecraftGameProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                MinecraftGameProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                _ => ProcessPriorityClass.Normal,
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            // Some Unix hosts do not allow changing priority without elevated privileges.
        }
    }

    private static async Task<int> CompleteAsync(Process process, ChannelWriter<GameProcessOutput> output)
    {
        try
        {
            var standardOutput = PumpAsync(process.StandardOutput, isError: false, output);
            var standardError = PumpAsync(process.StandardError, isError: true, output);
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            return process.ExitCode;
        }
        finally
        {
            output.TryComplete();
            process.Dispose();
        }
    }

    private static async Task PumpAsync(StreamReader reader, bool isError, ChannelWriter<GameProcessOutput> output)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            await output.WriteAsync(new GameProcessOutput(isError, line)).ConfigureAwait(false);
        }
    }
}
