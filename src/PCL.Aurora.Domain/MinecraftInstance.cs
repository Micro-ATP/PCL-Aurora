namespace PCL.Aurora.Domain;

public sealed record MinecraftInstance(
    string Name,
    string DirectoryPath,
    string? VersionId,
    string? Type,
    DateTimeOffset? ReleaseTime,
    MinecraftInstanceStatus Status);
