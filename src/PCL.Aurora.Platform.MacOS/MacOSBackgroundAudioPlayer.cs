using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSBackgroundAudioPlayer : IBackgroundAudioPlayer
{
    private const int SigStop = 17;
    private const int SigCont = 19;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stateLock = new();
    private Process? process;
    private int playbackGeneration;
    private double volume = 0.5;

    public bool IsSupported => OperatingSystem.IsMacOS() && File.Exists("/usr/bin/afplay");

    public bool SupportsSystemMediaControls => false;

    public bool IsPlaying
    {
        get
        {
            lock (stateLock)
            {
                try
                {
                    return process is { HasExited: false } && !IsPaused;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }

    public bool IsPaused { get; private set; }

    public string? CurrentPath { get; private set; }

    public event EventHandler? PlaybackEnded;

    public async Task PlayAsync(string path, double requestedVolume, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsSupported) throw new PlatformNotSupportedException("当前系统没有可用的背景音乐播放器。");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("背景音乐文件不存在。", fullPath);

        await gate.WaitAsync(cancellationToken);
        try
        {
            StopProcess(suppressEndedEvent: true);
            volume = Math.Clamp(requestedVolume, 0, 1);
            CurrentPath = fullPath;
            IsPaused = false;
            var generation = ++playbackGeneration;
            var nextProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/afplay",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            nextProcess.StartInfo.ArgumentList.Add("-v");
            nextProcess.StartInfo.ArgumentList.Add(volume.ToString("0.###", CultureInfo.InvariantCulture));
            nextProcess.StartInfo.ArgumentList.Add(fullPath);
            nextProcess.Exited += (_, _) => HandleExited(nextProcess, generation);
            if (!nextProcess.Start())
            {
                nextProcess.Dispose();
                CurrentPath = null;
                throw new InvalidOperationException("无法启动 macOS 背景音乐播放器。");
            }
            lock (stateLock)
            {
                process = nextProcess;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Process? current;
            lock (stateLock)
            {
                current = process;
            }
            if (current is null || IsPaused) return;
            try
            {
                if (current.HasExited) return;
                if (Kill(current.Id, SigStop) != 0) throw new InvalidOperationException("无法暂停背景音乐。");
            }
            catch (InvalidOperationException) when (!ReferenceEquals(process, current))
            {
                return;
            }
            lock (stateLock)
            {
                if (ReferenceEquals(process, current)) IsPaused = true;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Process? current;
            lock (stateLock)
            {
                current = process;
            }
            if (current is null || !IsPaused) return;
            try
            {
                if (current.HasExited) return;
                if (Kill(current.Id, SigCont) != 0) throw new InvalidOperationException("无法继续背景音乐。");
            }
            catch (InvalidOperationException) when (!ReferenceEquals(process, current))
            {
                return;
            }
            lock (stateLock)
            {
                if (ReferenceEquals(process, current)) IsPaused = false;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            StopProcess(suppressEndedEvent: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SetVolumeAsync(double requestedVolume, CancellationToken cancellationToken = default)
    {
        var normalized = Math.Clamp(requestedVolume, 0, 1);
        string? path;
        bool wasPaused;
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (Math.Abs(volume - normalized) < 0.001) return;
            volume = normalized;
            path = CurrentPath;
            wasPaused = IsPaused;
        }
        finally
        {
            gate.Release();
        }

        if (path is null) return;
        await PlayAsync(path, normalized, cancellationToken);
        if (wasPaused) await PauseAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        gate.Dispose();
    }

    private void HandleExited(Process exitedProcess, int generation)
    {
        var shouldNotify = false;
        lock (stateLock)
        {
            if (generation == playbackGeneration && ReferenceEquals(process, exitedProcess))
            {
                process = null;
                CurrentPath = null;
                IsPaused = false;
                shouldNotify = true;
            }
        }
        exitedProcess.Dispose();
        if (shouldNotify) PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void StopProcess(bool suppressEndedEvent)
    {
        Process? current;
        lock (stateLock)
        {
            current = process;
            if (current is null) return;
            if (suppressEndedEvent) playbackGeneration++;
            process = null;
            CurrentPath = null;
            IsPaused = false;
        }
        try
        {
            if (!current.HasExited) current.Kill();
        }
        catch (InvalidOperationException)
        {
        }
        current.Dispose();
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);
}
