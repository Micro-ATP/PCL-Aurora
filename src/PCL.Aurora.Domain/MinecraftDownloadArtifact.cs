namespace PCL.Aurora.Domain;

public sealed record MinecraftDownloadArtifact(
    string Description,
    string RelativePath,
    Uri Url,
    string? Sha1,
    long? Size);
