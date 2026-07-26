namespace PCL.Aurora.Domain;

public sealed record MinecraftInstalledLoader(
    MinecraftLoaderKind Kind,
    string? Version,
    string? MinecraftVersion);
