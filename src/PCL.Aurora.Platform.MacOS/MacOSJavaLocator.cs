using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed partial class MacOSJavaLocator : IJavaLocator
{
    private const string JavaHomeTool = "/usr/libexec/java_home";
    private const string SystemVirtualMachinesDirectory = "/Library/Java/JavaVirtualMachines";

    public async Task<IReadOnlyList<JavaInstallation>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<JavaCandidate>();
        var javaHomeToolCandidates = (await FindJavaHomeToolCandidatesAsync(cancellationToken).ConfigureAwait(false)).ToList();
        candidates.AddRange(FindJavaHomeCandidate());
        candidates.AddRange(FindDirectoryCandidates(SystemVirtualMachinesDirectory, JavaSource.SystemDirectory));
        candidates.AddRange(FindDirectoryCandidates(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Java", "JavaVirtualMachines"),
            JavaSource.UserDirectory));
        candidates.AddRange(FindPathCandidates().Where(candidate =>
            !IsMacOSJavaShim(candidate.ExecutablePath) || javaHomeToolCandidates.Count == 0));
        candidates.AddRange(javaHomeToolCandidates);

        var uniqueCandidates = candidates
            .Where(candidate => File.Exists(candidate.ExecutablePath))
            .GroupBy(candidate => NormalizePath(candidate.ExecutablePath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var installations = new List<JavaInstallation>(uniqueCandidates.Count);
        foreach (var candidate in uniqueCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installation = await InspectJavaAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (installation is not null)
            {
                installations.Add(installation);
            }
        }

        return installations
            .GroupBy(installation => installation.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(installation => installation.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<JavaCandidate> FindJavaHomeCandidate()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        return string.IsNullOrWhiteSpace(javaHome)
            ? []
            : [new JavaCandidate(Path.Combine(javaHome, "bin", "java"), JavaSource.JavaHome)];
    }

    private static IEnumerable<JavaCandidate> FindDirectoryCandidates(string rootDirectory, JavaSource source)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(rootDirectory)
            .Select(directory => new JavaCandidate(Path.Combine(directory, "Contents", "Home", "bin", "java"), source));
    }

    private static IEnumerable<JavaCandidate> FindPathCandidates()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => new JavaCandidate(Path.Combine(directory, "java"), JavaSource.Path));
    }

    private static async Task<IEnumerable<JavaCandidate>> FindJavaHomeToolCandidatesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(JavaHomeTool))
        {
            return [];
        }

        var result = await RunProcessAsync(JavaHomeTool, ["-V"], cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return [];
        }

        return result.Output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JavaHomePathPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => new JavaCandidate(Path.Combine(match.Groups["home"].Value, "bin", "java"), JavaSource.JavaHomeTool));
    }

    private static async Task<JavaInstallation?> InspectJavaAsync(JavaCandidate candidate, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(candidate.ExecutablePath, ["-XshowSettings:properties", "-version"], cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return null;
        }

        var version = VersionPattern().Match(result.Output).Groups["version"].Value;
        var architecture = ParseArchitecture(result.Output);
        var compatible = architecture is JavaArchitecture.Unknown
            || architecture == ToJavaArchitecture(RuntimeInformation.OSArchitecture);

        return new JavaInstallation(
            ExecutablePath: GetCanonicalExecutablePath(result.Output, candidate.ExecutablePath),
            Version: string.IsNullOrEmpty(version) ? null : version,
            MajorVersion: JavaVersion.ParseMajorVersion(version),
            Vendor: ParseVendor(result.Output),
            Architecture: architecture,
            Source: candidate.Source,
            IsCompatible: compatible);
    }

    private static async Task<ProcessResult?> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await standardOutput.ConfigureAwait(false) + await standardError.ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JavaArchitecture ParseArchitecture(string output)
    {
        var architecture = ArchitecturePattern().Match(output).Groups["architecture"].Value;
        return architecture.ToLowerInvariant() switch
        {
            "aarch64" or "arm64" => JavaArchitecture.Arm64,
            "amd64" or "x86_64" or "x64" => JavaArchitecture.X64,
            _ => JavaArchitecture.Unknown,
        };
    }

    private static string ParseVendor(string output)
    {
        var vendor = VendorPattern().Match(output).Groups["vendor"].Value.Trim();
        return string.IsNullOrWhiteSpace(vendor) ? "未知供应商" : vendor;
    }

    private static string GetCanonicalExecutablePath(string output, string fallbackPath)
    {
        var javaHome = JavaHomeSettingPattern().Match(output).Groups["home"].Value.Trim();
        if (string.IsNullOrWhiteSpace(javaHome))
        {
            return NormalizePath(fallbackPath);
        }

        var executablePath = Path.Combine(javaHome, "bin", "java");
        return File.Exists(executablePath) ? NormalizePath(executablePath) : NormalizePath(fallbackPath);
    }

    private static JavaArchitecture ToJavaArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => JavaArchitecture.Arm64,
        Architecture.X64 => JavaArchitecture.X64,
        _ => JavaArchitecture.Unknown,
    };

    private static string NormalizePath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath) ?? string.Empty;
            var resolvedPath = root;
            foreach (var segment in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                resolvedPath = Path.Combine(resolvedPath, segment);
                FileSystemInfo item = Directory.Exists(resolvedPath)
                    ? new DirectoryInfo(resolvedPath)
                    : new FileInfo(resolvedPath);
                var target = item.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    resolvedPath = target.FullName;
                }
            }

            return resolvedPath;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    private static bool IsMacOSJavaShim(string path) => string.Equals(
        Path.GetFullPath(path),
        "/usr/bin/java",
        StringComparison.Ordinal);

    [GeneratedRegex("(?<home>/[^\\r\\n]+)$")]
    private static partial Regex JavaHomePathPattern();

    [GeneratedRegex("(?:java|openjdk) version \\\"(?<version>[^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^\\s*os\\.arch\\s*=\\s*(?<architecture>[^\\s]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ArchitecturePattern();

    [GeneratedRegex("^\\s*java\\.vendor\\s*=\\s*(?<vendor>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex VendorPattern();

    [GeneratedRegex("^\\s*java\\.home\\s*=\\s*(?<home>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex JavaHomeSettingPattern();

    private sealed record JavaCandidate(string ExecutablePath, JavaSource Source);

    private sealed record ProcessResult(int ExitCode, string Output);
}
