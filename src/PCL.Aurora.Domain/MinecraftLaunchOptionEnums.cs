namespace PCL.Aurora.Domain;

public enum MinecraftInstanceIsolationMode
{
    Disabled = 0,
    ModdedOnly = 1,
    NonReleaseOnly = 2,
    ModdedAndNonRelease = 3,
    All = 4,
}

public enum MinecraftLauncherVisibility
{
    ExitImmediately = 0,
    HideAndExit = 2,
    HideAndReopen = 3,
    MinimizeAndReopen = 4,
    DoNothing = 5,
}

public enum MinecraftGameProcessPriority
{
    AboveNormal = 0,
    Normal = 1,
    BelowNormal = 2,
    High = 3,
    RealTime = 4,
}

public enum MinecraftPreferredIpStack
{
    PreferIpv4 = 0,
    JavaDefault = 1,
    PreferIpv6 = 2,
}

public enum MinecraftRendererMode
{
    GameDefault = 0,
    Software = 1,
    DirectX12 = 2,
    Vulkan = 3,
}
