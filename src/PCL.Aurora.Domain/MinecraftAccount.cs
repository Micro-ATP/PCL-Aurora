namespace PCL.Aurora.Domain;

public sealed record MinecraftAccount(
    string DisplayName,
    string Uuid,
    MinecraftAccountKind Kind,
    bool IsAuthenticated);
