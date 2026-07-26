using System.Diagnostics;
using System.Threading.Channels;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftGameProcessRunner : IGameProcessRunner
{
    public Task<GameProcessSession> StartAsync(
        MinecraftGameLaunchRequest request,
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

        var output = Channel.CreateUnbounded<GameProcessOutput>();
        return Task.FromResult(new GameProcessSession(
            process.Id,
            output.Reader,
            CompleteAsync(process, output.Writer)));
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
