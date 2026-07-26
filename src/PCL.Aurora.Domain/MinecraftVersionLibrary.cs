namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionLibrary(
    string Name,
    string? ArtifactPath,
    MinecraftVersionDownload? Artifact,
    bool HasConditionalRules);
