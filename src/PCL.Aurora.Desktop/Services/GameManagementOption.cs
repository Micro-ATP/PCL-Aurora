namespace PCL.Aurora.Desktop.Services;

public sealed record GameManagementOption<T>(T Value, string DisplayName)
    where T : struct, Enum;
