namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetIndexParseResult(MinecraftAssetIndex? Index, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Index is not null && Errors.Count == 0;
}
