using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record ModrinthModpackImportResult(
    string TargetDirectory,
    string MinecraftVersion,
    MinecraftLoaderKind? LoaderKind,
    string? LoaderVersion,
    int DownloadedFileCount,
    int OverrideFileCount);
