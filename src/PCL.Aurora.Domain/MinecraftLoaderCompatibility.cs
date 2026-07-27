namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderCompatibility(bool IsCompatible, IReadOnlyList<string> Reasons);

public static class MinecraftLoaderCompatibilityEvaluator
{
    public static MinecraftLoaderCompatibility Evaluate(
        string minecraftVersion,
        IReadOnlyCollection<MinecraftLoaderCatalogEntry> selectedLoaders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);
        ArgumentNullException.ThrowIfNull(selectedLoaders);

        var reasons = new List<string>();
        if (selectedLoaders.Count > 1)
        {
            reasons.Add("一次只能选择一个加载器安装器。Forge、NeoForge、Fabric 与 OptiFine 不能同时安装。");
        }

        foreach (var selected in selectedLoaders)
        {
            if (!string.Equals(selected.MinecraftVersion, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"{selected.Kind} {selected.Version} 仅适用于 Minecraft {selected.MinecraftVersion}。");
            }
        }

        return new(reasons.Count == 0, reasons);
    }
}
