using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MicrosoftAccountRestoreResult(MinecraftAccount? Account, string? Warning);
