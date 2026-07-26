// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModMinecraft.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: extracts only Forge, NeoForge and Fabric
// coordinates from already-local metadata; omits PCL's Windows UI, cache and other loaders.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class MinecraftInstalledLoaderDetector
{
    public static MinecraftInstalledLoader? Detect(IEnumerable<string> libraryNames)
    {
        ArgumentNullException.ThrowIfNull(libraryNames);
        var names = libraryNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();

        // Preserve PCL2's visible classification order: Fabric, Forge (not NeoForge), NeoForge.
        var fabric = names.FirstOrDefault(name => name.StartsWith("net.fabricmc:fabric-loader:", StringComparison.OrdinalIgnoreCase));
        if (fabric is not null)
        {
            return new(MinecraftLoaderKind.Fabric, GetCoordinateSegment(fabric, 2), null);
        }

        var forge = names.FirstOrDefault(name =>
            (name.StartsWith("net.minecraftforge:forge:", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("net.minecraftforge:fmlloader:", StringComparison.OrdinalIgnoreCase)) &&
            !name.Contains("neoforge", StringComparison.OrdinalIgnoreCase));
        if (forge is not null)
        {
            var coordinateVersion = GetCoordinateSegment(forge, 2);
            var separator = coordinateVersion?.IndexOf('-') ?? -1;
            return new(
                MinecraftLoaderKind.Forge,
                separator >= 0 ? coordinateVersion![(separator + 1)..] : null,
                separator >= 0 ? coordinateVersion![..separator] : null);
        }

        var neoForge = names.FirstOrDefault(name =>
            name.StartsWith("net.neoforged:neoforge:", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("net.neoforged:forge:", StringComparison.OrdinalIgnoreCase));
        if (neoForge is not null)
        {
            var coordinateVersion = GetCoordinateSegment(neoForge, 2);
            var separator = coordinateVersion?.IndexOf('-') ?? -1;
            var isLegacy = neoForge.StartsWith("net.neoforged:forge:", StringComparison.OrdinalIgnoreCase);
            return new(
                MinecraftLoaderKind.NeoForge,
                isLegacy && separator >= 0 ? coordinateVersion![(separator + 1)..] : coordinateVersion,
                isLegacy && separator >= 0 ? coordinateVersion![..separator] : null);
        }

        return null;
    }

    private static string? GetCoordinateSegment(string coordinate, int index)
    {
        var segments = coordinate.Split(':');
        return segments.Length > index && !string.IsNullOrWhiteSpace(segments[index]) ? segments[index] : null;
    }
}
