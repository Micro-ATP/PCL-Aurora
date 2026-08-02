using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public enum MinecraftInstanceContentKind
{
    Mod,
    ResourcePack,
    ShaderPack,
    Save,
    Screenshot,
    Schematic,
}

public sealed record MinecraftInstanceContentEntry(
    MinecraftInstanceContentKind Kind,
    string Name,
    string RelativePath,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset LastModified,
    bool IsEnabled,
    string Detail)
{
    public bool CanToggle => Kind == MinecraftInstanceContentKind.Mod;
}

public sealed record MinecraftServerEntry(
    string Name,
    string Address,
    string? Icon = null,
    bool? AcceptTextures = null,
    bool Hidden = false)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 128 &&
        !string.IsNullOrWhiteSpace(Address) && Address.Length <= 512 &&
        !Name.Any(char.IsControl) && !Address.Any(char.IsControl);
}

public sealed record MinecraftInstanceProfile(
    string Description = "",
    bool IsFavorite = false,
    MinecraftInstanceIsolationMode? IsolationMode = null)
{
    public const int MaximumDescriptionLength = 400;

    public static MinecraftInstanceProfile Default { get; } = new();

    public bool IsValid =>
        Description.Length <= MaximumDescriptionLength &&
        (IsolationMode is null || Enum.IsDefined(IsolationMode.Value));
}

public sealed record MinecraftInstanceManagementSnapshot(
    MinecraftInstance Instance,
    string MinecraftRootDirectory,
    string GameDirectory,
    MinecraftInstanceIsolationMode EffectiveIsolationMode,
    MinecraftInstanceProfile Profile,
    IReadOnlyDictionary<MinecraftInstanceContentKind, int> ContentCounts,
    int ServerCount)
{
    public int GetCount(MinecraftInstanceContentKind kind) =>
        ContentCounts.TryGetValue(kind, out var count) ? count : 0;
}

public sealed record MinecraftInstanceImportResult(int ImportedCount, IReadOnlyList<string> ImportedNames);

public sealed record MinecraftInstanceArchiveResult(string ArchivePath, int FileCount, long TotalBytes);
