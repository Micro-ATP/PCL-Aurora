using PCL.Aurora.Domain;
using System.Text.Json;

namespace PCL.Aurora.Application;

/// <summary>
/// 旧版 OptiFine 继承版本的库坐标、版本 JSON 和 Tweaker 语义直接适配自 PCL-CE 的
/// Plain Craft Launcher 2/Pages/PageDownload/ModDownloadLib.cs；文件写入改为 Aurora 的原子跨平台实现。
/// </summary>
public sealed class MinecraftLoaderInstallerService(
    HttpClient httpClient,
    IMinecraftDownloadExecutor downloadExecutor,
    IMinecraftLoaderInstallerProcessRunner processRunner) : IMinecraftLoaderInstallerService
{
    private static readonly Uri FabricInstallerCatalogUri = new("https://meta.fabricmc.net/v2/versions/installer");

    public async Task<MinecraftLoaderInstallerPlan> PrepareAsync(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        JavaInstallation? java,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        Uri? fabricInstallerUri = null;
        if (loader.Kind == MinecraftLoaderKind.Fabric)
        {
            try
            {
                using var response = await httpClient.GetAsync(FabricInstallerCatalogUri, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                fabricInstallerUri = MinecraftFabricInstallerMetadataParser.ParseLatestStableInstallerUri(content);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException)
            {
                return new(loader, null, null, [$"无法获取 Fabric 官方安装器目录：{exception.Message}"]);
            }
        }

        MinecraftLegacyOptiFineInstallation? legacyOptiFineInstallation = null;
        if (MinecraftLoaderInstallerPlanBuilder.IsLegacyOptiFine(loader))
        {
            var legacyPreparation = await PrepareLegacyOptiFineAsync(loader, minecraftRootDirectory, cancellationToken).ConfigureAwait(false);
            if (legacyPreparation.Error is not null)
            {
                return new(loader, null, null, [legacyPreparation.Error]);
            }

            legacyOptiFineInstallation = legacyPreparation.Installation;
        }

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(
            loader,
            minecraftRootDirectory,
            java,
            fabricInstallerUri,
            legacyOptiFineInstallation);
        if (!plan.CanInstall || plan.InstallerArtifact?.Sha1Url is not { } sha1Url)
        {
            return plan;
        }

        try
        {
            using var response = await httpClient.GetAsync(sha1Url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var sha1 = ParseSha1(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return sha1 is null
                ? plan with { InstallerArtifact = null, ProcessRequest = null, BlockingReasons = ["官方安装器校验文件无效；未下载或执行安装器。"] }
                : plan with { InstallerArtifact = plan.InstallerArtifact with { Sha1 = sha1 } };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return plan with
            {
                InstallerArtifact = null,
                ProcessRequest = null,
                BlockingReasons = [$"无法获取官方安装器 SHA-1 校验文件：{exception.Message}"],
            };
        }
    }

    public async Task<MinecraftLoaderInstallerExecutionResult> InstallAsync(
        MinecraftLoaderInstallerPlan plan,
        string minecraftRootDirectory,
        bool hasExplicitUserConfirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!hasExplicitUserConfirmation)
        {
            return new(null, [], ["加载器安装需要用户明确确认；未下载或执行安装器。"]);
        }

        if (!plan.CanInstall)
        {
            return new(null, [], plan.BlockingReasons.Count == 0 ? ["加载器安装计划不完整。"] : plan.BlockingReasons);
        }

        try
        {
            var downloadPlan = new MinecraftDownloadPlan(
                $"loader-installer:{plan.Loader.Kind}:{plan.Loader.Version}",
                [plan.InstallerArtifact!],
                []);
            await downloadExecutor.ExecuteAsync(downloadPlan, minecraftRootDirectory, cancellationToken).ConfigureAwait(false);
            if (plan.LegacyOptiFineInstallation is { } legacyOptiFine)
            {
                await InstallLegacyOptiFineAsync(minecraftRootDirectory, legacyOptiFine, cancellationToken).ConfigureAwait(false);
                return new(0, [], []);
            }

            return await processRunner.ExecuteAsync(plan.ProcessRequest!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or InvalidDataException or InvalidOperationException)
        {
            return new(null, [], [$"加载器安装未完成：{exception.Message}"]);
        }
    }

    private static string? ParseSha1(string content)
    {
        var value = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return value is { Length: 40 } && value.All(Uri.IsHexDigit) ? value : null;
    }

    private static async Task<(MinecraftLegacyOptiFineInstallation? Installation, string? Error)> PrepareLegacyOptiFineAsync(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        CancellationToken cancellationToken)
    {
        if (!TryGetSafeRoot(minecraftRootDirectory, out var rootDirectory, out var rootError))
        {
            return (null, rootError);
        }

        if (!IsSafeVersionId(loader.MinecraftVersion))
        {
            return (null, "旧版 OptiFine 的基础 Minecraft 版本名称无效。 ");
        }

        var metadataPath = GetPathWithinRoot(rootDirectory!, $"versions/{loader.MinecraftVersion}/{loader.MinecraftVersion}.json");
        var clientJarPath = GetPathWithinRoot(rootDirectory!, $"versions/{loader.MinecraftVersion}/{loader.MinecraftVersion}.jar");
        if (metadataPath is null || clientJarPath is null ||
            HasSymbolicLinkInExistingPath(rootDirectory!, metadataPath) ||
            HasSymbolicLinkInExistingPath(rootDirectory!, clientJarPath))
        {
            return (null, "基础 Minecraft 目录包含不安全路径或符号链接。 ");
        }

        if (!File.Exists(metadataPath) || !File.Exists(clientJarPath))
        {
            return (null, "旧版 OptiFine 需要已安装的基础 Minecraft JSON 和客户端 JAR；未下载或写入任何文件。 ");
        }

        try
        {
            var parsed = MinecraftVersionMetadataParser.Parse(
                await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            if (!parsed.IsSuccess || parsed.Metadata is null)
            {
                return (null, "基础 Minecraft 元数据无法安全解析：" + string.Join("；", parsed.Errors));
            }

            if (!MinecraftLegacyOptiFineInstallation.TryCreate(loader, parsed.Metadata, out var installation, out var error))
            {
                return (null, error ?? "无法生成旧版 OptiFine 继承版本计划。 ");
            }

            var libraryPath = GetPathWithinRoot(rootDirectory!, installation!.LibraryRelativePath);
            var versionDirectory = GetPathWithinRoot(rootDirectory!, $"versions/{installation.VersionId}");
            if (libraryPath is null || versionDirectory is null ||
                HasSymbolicLinkInExistingPath(rootDirectory!, libraryPath) ||
                HasSymbolicLinkInExistingPath(rootDirectory!, versionDirectory))
            {
                return (null, "旧版 OptiFine 安装目标包含不安全路径或符号链接。 ");
            }

            if (File.Exists(libraryPath) || Directory.Exists(libraryPath) ||
                File.Exists(versionDirectory) || Directory.Exists(versionDirectory))
            {
                return (null, "旧版 OptiFine 的库或派生版本已存在，不会覆盖。 ");
            }

            return (installation, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"无法读取基础 Minecraft 元数据：{exception.Message}");
        }
    }

    private static async Task InstallLegacyOptiFineAsync(
        string minecraftRootDirectory,
        MinecraftLegacyOptiFineInstallation installation,
        CancellationToken cancellationToken)
    {
        if (!TryGetSafeRoot(minecraftRootDirectory, out var rootDirectory, out var rootError))
        {
            throw new InvalidOperationException(rootError);
        }

        var baseJarPath = GetPathWithinRoot(rootDirectory!, $"versions/{installation.BaseVersionId}/{installation.BaseVersionId}.jar");
        var libraryPath = GetPathWithinRoot(rootDirectory!, installation.LibraryRelativePath);
        var versionsDirectory = GetPathWithinRoot(rootDirectory!, "versions");
        var versionDirectory = GetPathWithinRoot(rootDirectory!, $"versions/{installation.VersionId}");
        if (baseJarPath is null || libraryPath is null || versionsDirectory is null || versionDirectory is null ||
            HasSymbolicLinkInExistingPath(rootDirectory!, baseJarPath) ||
            HasSymbolicLinkInExistingPath(rootDirectory!, libraryPath) ||
            HasSymbolicLinkInExistingPath(rootDirectory!, versionsDirectory))
        {
            throw new InvalidOperationException("旧版 OptiFine 安装目标包含不安全路径或符号链接。 ");
        }

        if (!File.Exists(baseJarPath) || !File.Exists(libraryPath))
        {
            throw new InvalidOperationException("旧版 OptiFine 所需的基础客户端或下载库文件不存在。 ");
        }

        if (Directory.Exists(versionDirectory) || File.Exists(versionDirectory))
        {
            throw new InvalidOperationException($"派生版本 {installation.VersionId} 已存在，不会覆盖。 ");
        }

        var temporaryDirectory = Path.Combine(versionsDirectory, $".{installation.VersionId}.{Guid.NewGuid():N}.partial");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            if (HasSymbolicLinkInExistingPath(rootDirectory!, temporaryDirectory))
            {
                throw new InvalidOperationException("无法在安全目录中创建旧版 OptiFine 临时版本。 ");
            }

            var temporaryJarPath = Path.Combine(temporaryDirectory, installation.VersionId + ".jar");
            await using (var source = new FileStream(baseJarPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var destination = new FileStream(temporaryJarPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            var temporaryMetadataPath = Path.Combine(temporaryDirectory, installation.VersionId + ".json");
            var json = CreateLegacyOptiFineMetadata(installation);
            await File.WriteAllTextAsync(temporaryMetadataPath, json, cancellationToken).ConfigureAwait(false);
            var parsed = MinecraftVersionMetadataParser.Parse(json);
            if (!parsed.IsSuccess || parsed.Metadata is null || !string.Equals(parsed.Metadata.Id, installation.VersionId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("生成的旧版 OptiFine 版本元数据未通过校验。 ");
            }

            Directory.Move(temporaryDirectory, versionDirectory);
        }
        catch
        {
            TryDeleteDirectory(temporaryDirectory);
            TryDeleteFile(libraryPath);
            throw;
        }
    }

    private static string CreateLegacyOptiFineMetadata(MinecraftLegacyOptiFineInstallation installation)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["id"] = installation.VersionId,
            ["inheritsFrom"] = installation.BaseVersionId,
            ["type"] = "release",
            ["libraries"] = new object[]
            {
                new Dictionary<string, string> { ["name"] = installation.LibraryCoordinate },
                new Dictionary<string, string> { ["name"] = "net.minecraft:launchwrapper:1.12" },
            },
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minimumLauncherVersion"] = installation.UsesLegacyGameArguments ? 18 : 21,
        };
        if (installation.UsesLegacyGameArguments)
        {
            metadata["minecraftArguments"] = installation.BaseLegacyGameArguments + " --tweakClass optifine.OptiFineTweaker";
        }
        else
        {
            metadata["arguments"] = new Dictionary<string, object[]>
            {
                ["game"] = ["--tweakClass", "optifine.OptiFineTweaker"],
            };
        }

        return JsonSerializer.Serialize(metadata);
    }

    private static bool TryGetSafeRoot(string minecraftRootDirectory, out string? rootDirectory, out string? error)
    {
        rootDirectory = null;
        error = null;
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory) || !Path.IsPathFullyQualified(minecraftRootDirectory))
        {
            error = "Minecraft 根目录必须是绝对路径。 ";
            return false;
        }

        rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        if (HasSymbolicLinkInExistingPath(rootDirectory, rootDirectory))
        {
            error = "Minecraft 根目录包含符号链接，旧版 OptiFine 安装已拒绝。 ";
            return false;
        }

        return true;
    }

    private static string? GetPathWithinRoot(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        return candidate.StartsWith(rootDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? candidate : null;
    }

    private static bool HasSymbolicLinkInExistingPath(string rootDirectory, string path)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, path);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return true;
        }

        var current = rootDirectory;
        if (IsSymbolicLink(current))
        {
            return true;
        }

        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrEmpty(segment) || segment == ".")
            {
                continue;
            }

            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
            {
                break;
            }

            if (IsSymbolicLink(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSymbolicLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        return !string.IsNullOrWhiteSpace(info.LinkTarget) || (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static bool IsSafeVersionId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id == Path.GetFileName(id) &&
        !id.Contains(Path.DirectorySeparatorChar) && !id.Contains(Path.AltDirectorySeparatorChar);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !IsSymbolicLink(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) && !IsSymbolicLink(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
