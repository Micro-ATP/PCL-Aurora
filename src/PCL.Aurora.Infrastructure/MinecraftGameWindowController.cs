using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

internal static class MinecraftGameWindowController
{
    private const int SwMaximize = 3;
    private static readonly IntPtr TopHandle = IntPtr.Zero;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    public static void BeginApply(int processId, MinecraftGameLaunchRequest request)
    {
        if (request.WindowMode == MinecraftGameWindowMode.Default &&
            string.IsNullOrWhiteSpace(request.WindowTitle))
        {
            return;
        }

        _ = Task.Run(() => ApplyAsync(processId, request));
    }

    private static async Task ApplyAsync(int processId, MinecraftGameLaunchRequest request)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                await ApplyWindowsAsync(processId, request).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsMacOS())
            {
                await ApplyMacOsAsync(processId, request).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsLinux())
            {
                await ApplyLinuxAsync(processId, request).ConfigureAwait(false);
            }
        }
        catch
        {
            // Window decoration and compositor policies differ by host. A failed
            // cosmetic operation must never terminate an otherwise valid game.
        }
    }

    private static async Task ApplyWindowsAsync(int processId, MinecraftGameLaunchRequest request)
    {
        var handle = IntPtr.Zero;
        for (var attempt = 0; attempt < 60 && handle == IntPtr.Zero; attempt++)
        {
            handle = FindWindowsWindow(processId);
            if (handle == IntPtr.Zero)
            {
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.WindowTitle))
        {
            _ = SetWindowText(handle, request.WindowTitle);
        }

        if (request.WindowMode == MinecraftGameWindowMode.Maximized)
        {
            _ = ShowWindow(handle, SwMaximize);
        }
        else if (request.WindowMode is MinecraftGameWindowMode.Custom or MinecraftGameWindowMode.Launcher)
        {
            _ = SetWindowPos(
                handle,
                TopHandle,
                0,
                0,
                request.WindowWidth,
                request.WindowHeight,
                NoZOrder | NoActivate);
        }
    }

    private static IntPtr FindWindowsWindow(int processId)
    {
        var result = IntPtr.Zero;
        _ = EnumWindows((handle, callbackParameter) =>
        {
            GetWindowThreadProcessId(handle, out var ownerProcessId);
            if (ownerProcessId == (uint)processId && IsWindowVisible(handle))
            {
                result = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static async Task ApplyMacOsAsync(int processId, MinecraftGameLaunchRequest request)
    {
        const string script = """
            on run argv
                set targetPid to item 1 of argv as integer
                set targetMode to item 2 of argv
                set targetWidth to item 3 of argv as integer
                set targetHeight to item 4 of argv as integer
                set targetTitle to item 5 of argv
                tell application "System Events"
                    set targetProcess to first application process whose unix id is targetPid
                    repeat 60 times
                        if exists window 1 of targetProcess then exit repeat
                        delay 0.25
                    end repeat
                    if not (exists window 1 of targetProcess) then return
                    if targetTitle is not "" then
                        try
                            set value of attribute "AXTitle" of window 1 of targetProcess to targetTitle
                        end try
                    end if
                    if targetMode is "Maximized" then
                        try
                            perform action "AXZoomWindow" of window 1 of targetProcess
                        end try
                    else if targetMode is "Custom" or targetMode is "Launcher" then
                        try
                            set size of window 1 of targetProcess to {targetWidth, targetHeight}
                        end try
                    end if
                end tell
            end run
            """;
        await RunCommandAsync(
            "/usr/bin/osascript",
            [
                "-e",
                script,
                processId.ToString(CultureInfo.InvariantCulture),
                request.WindowMode.ToString(),
                request.WindowWidth.ToString(CultureInfo.InvariantCulture),
                request.WindowHeight.ToString(CultureInfo.InvariantCulture),
                request.WindowTitle ?? string.Empty,
            ]).ConfigureAwait(false);
    }

    private static async Task ApplyLinuxAsync(int processId, MinecraftGameLaunchRequest request)
    {
        var wmctrl = FindExecutable("wmctrl");
        if (wmctrl is not null)
        {
            string? windowId = null;
            for (var attempt = 0; attempt < 60 && windowId is null; attempt++)
            {
                var output = await RunCommandAsync(wmctrl, ["-lp"]).ConfigureAwait(false);
                windowId = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length >= 3 &&
                                    int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var owner) &&
                                    owner == processId)
                    .Select(parts => parts[0])
                    .FirstOrDefault();
                if (windowId is null)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }

            if (windowId is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(request.WindowTitle))
            {
                await RunCommandAsync(wmctrl, ["-ir", windowId, "-T", request.WindowTitle]).ConfigureAwait(false);
            }

            if (request.WindowMode == MinecraftGameWindowMode.Maximized)
            {
                await RunCommandAsync(
                    wmctrl,
                    ["-ir", windowId, "-b", "add,maximized_vert,maximized_horz"]).ConfigureAwait(false);
            }
            else if (request.WindowMode is MinecraftGameWindowMode.Custom or MinecraftGameWindowMode.Launcher)
            {
                await RunCommandAsync(
                    wmctrl,
                    ["-ir", windowId, "-e", $"0,-1,-1,{request.WindowWidth},{request.WindowHeight}"]).ConfigureAwait(false);
            }

            return;
        }

        var xdotool = FindExecutable("xdotool");
        if (xdotool is null)
        {
            return;
        }

        string? xWindowId = null;
        for (var attempt = 0; attempt < 60 && string.IsNullOrWhiteSpace(xWindowId); attempt++)
        {
            xWindowId = (await RunCommandAsync(
                xdotool,
                ["search", "--onlyvisible", "--pid", processId.ToString(CultureInfo.InvariantCulture)])
                .ConfigureAwait(false))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(xWindowId))
            {
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(xWindowId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.WindowTitle))
        {
            await RunCommandAsync(xdotool, ["set_window", "--name", request.WindowTitle, xWindowId]).ConfigureAwait(false);
        }

        if (request.WindowMode is MinecraftGameWindowMode.Custom or MinecraftGameWindowMode.Launcher)
        {
            await RunCommandAsync(
                xdotool,
                ["windowsize", xWindowId, request.WindowWidth.ToString(CultureInfo.InvariantCulture), request.WindowHeight.ToString(CultureInfo.InvariantCulture)])
                .ConfigureAwait(false);
        }
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        return path?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    private static async Task<string> RunCommandAsync(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return string.Empty;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return await outputTask.ConfigureAwait(false);
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowText(IntPtr handle, string text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
