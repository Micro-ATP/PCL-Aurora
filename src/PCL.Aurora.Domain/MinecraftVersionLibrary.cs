namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionLibrary(
    string Name,
    string? ArtifactPath,
    MinecraftVersionDownload? Artifact,
    bool HasConditionalRules,
    IReadOnlyDictionary<string, string>? NativeClassifiers = null,
    IReadOnlyDictionary<string, MinecraftVersionLibraryClassifier>? Classifiers = null,
    IReadOnlyList<MinecraftLaunchRule>? Rules = null,
    bool HasUnsupportedRules = false);
