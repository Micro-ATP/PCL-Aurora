namespace PCL.Aurora.Domain;

/// <summary>
/// 旧版 OptiFine 的继承版本布局直接适配自 PCL-CE 的
/// Plain Craft Launcher 2/Pages/PageDownload/ModDownloadLib.cs。
/// Aurora 仅保留跨平台版本名、Maven 库坐标和启动元数据语义。
/// </summary>
public sealed record MinecraftLegacyOptiFineInstallation(
    string BaseVersionId,
    string VersionId,
    string LibraryRelativePath,
    string LibraryCoordinate,
    bool UsesLegacyGameArguments,
    string? BaseLegacyGameArguments)
{
    public static bool TryCreate(
        MinecraftLoaderCatalogEntry loader,
        MinecraftVersionMetadata baseMetadata,
        out MinecraftLegacyOptiFineInstallation? installation,
        out string? error)
    {
        installation = null;
        error = null;
        if (loader.Kind != MinecraftLoaderKind.OptiFine || loader.OptiFineEntry is not { } optiFine)
        {
            error = "旧版 OptiFine 安装计划缺少目录条目。";
            return false;
        }

        var fileName = optiFine.FileName;
        if (!fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            error = "旧版 OptiFine 文件名无效。";
            return false;
        }

        var normalizedFileName = fileName.StartsWith("preview_", StringComparison.OrdinalIgnoreCase)
            ? fileName["preview_".Length..]
            : fileName;
        if (!normalizedFileName.StartsWith("OptiFine_", StringComparison.Ordinal) ||
            normalizedFileName.Length <= "OptiFine_.jar".Length)
        {
            error = "旧版 OptiFine 文件名不符合公开目录格式。";
            return false;
        }

        var mavenVersion = normalizedFileName["OptiFine_".Length..^".jar".Length];
        var basePrefix = loader.MinecraftVersion + "_";
        if (!mavenVersion.StartsWith(basePrefix, StringComparison.Ordinal) ||
            !IsSafeToken(mavenVersion) ||
            !string.Equals(baseMetadata.Id, loader.MinecraftVersion, StringComparison.Ordinal))
        {
            error = "旧版 OptiFine 与基础 Minecraft 版本元数据不匹配。";
            return false;
        }

        var suffix = mavenVersion[basePrefix.Length..];
        if (!IsSafeToken(suffix) || baseMetadata.Launch is not { } launch ||
            (launch.HasModernArguments is false && string.IsNullOrWhiteSpace(launch.LegacyGameArguments)))
        {
            error = "基础 Minecraft 元数据不包含 Aurora 可安全继承的启动参数。";
            return false;
        }

        installation = new(
            loader.MinecraftVersion,
            $"{loader.MinecraftVersion}-OptiFine_{suffix}",
            $"libraries/optifine/OptiFine/{mavenVersion}/OptiFine-{mavenVersion}.jar",
            $"optifine:OptiFine:{mavenVersion}",
            UsesLegacyGameArguments: !launch.HasModernArguments,
            BaseLegacyGameArguments: launch.LegacyGameArguments);
        return true;
    }

    private static bool IsSafeToken(string value) =>
        value.Length is > 0 and <= 160 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');
}
