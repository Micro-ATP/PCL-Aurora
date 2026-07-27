// Directly adapted from PCL-CE, Plain Craft Launcher 2/Pages/PageSetup/PageSetupLaunch.xaml.cs.
// Copyright © LtcCat.
// Modified by Micro-ATP for PCL Aurora: accepts platform-neutral memory facts and emits
// an explicit MiB allocation; it does not access Windows kernel APIs or alter system memory.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class PclCeMinecraftMemoryAllocator
{
    private const long BytesPerMiB = 1024L * 1024L;
    private const long BytesPerGiB = BytesPerMiB * 1024L;

    public static MinecraftMemoryAllocationPreparation Prepare(
        MinecraftLaunchOptions options,
        long totalMemoryBytes,
        long availableMemoryBytes,
        MinecraftInstance? instance,
        int modCount,
        JavaInstallation? java)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid)
        {
            return new(null, ["内存设置包含不支持的值。"]);
        }

        if (options.MemoryAllocationMode == MinecraftMemoryAllocationMode.Automatic &&
            (totalMemoryBytes <= 0 || availableMemoryBytes <= 0))
        {
            return new(null, ["无法获取可用物理内存，无法安全计算 Minecraft 堆大小。"]);
        }

        var allocationMiB = options.MemoryAllocationMode switch
        {
            MinecraftMemoryAllocationMode.Automatic => CalculateAutomaticMiB(availableMemoryBytes, instance, modCount),
            MinecraftMemoryAllocationMode.Custom => options.CustomMemoryMiB,
            _ => 0,
        };
        if (allocationMiB <= 0)
        {
            return new(null, ["内存分配模式无效。"]);
        }

        var is32BitJava = java?.Architecture is JavaArchitecture.X86 or JavaArchitecture.Unknown;
        if (is32BitJava)
        {
            allocationMiB = Math.Min(allocationMiB, 1024);
        }

        return new(
            new MinecraftMemoryAllocation(
                allocationMiB,
                options.MemoryAllocationMode == MinecraftMemoryAllocationMode.Automatic,
                is32BitJava),
            []);
    }

    private static int CalculateAutomaticMiB(long availableMemoryBytes, MinecraftInstance? instance, int modCount)
    {
        var availableGiB = Math.Round((double)availableMemoryBytes / BytesPerGiB, 1, MidpointRounding.AwayFromZero);
        var isModded = instance?.InstalledLoader is not null;
        var minimumGiB = isModded ? 0.5d + Math.Max(modCount, 0) / 150d : 0.5d;
        var target1GiB = isModded ? 1.5d + Math.Max(modCount, 0) / 90d : 1.5d;
        var target2GiB = isModded ? 2.7d + Math.Max(modCount, 0) / 50d : 2.5d;
        var target3GiB = isModded ? 4.5d + Math.Max(modCount, 0) / 25d : 4d;
        var resultGiB = 0d;
        foreach (var (delta, ratio) in new[]
                 {
                     (target1GiB, 1d),
                     (target2GiB - target1GiB, 0.7d),
                     (target3GiB - target2GiB, 0.4d),
                     (target3GiB, 0.15d),
                 })
        {
            resultGiB += Math.Min(availableGiB * ratio, delta);
            availableGiB -= delta / ratio;
            if (availableGiB < 0.1d)
            {
                break;
            }
        }

        resultGiB = Math.Round(Math.Max(resultGiB, minimumGiB), 1, MidpointRounding.AwayFromZero);
        return Math.Max(1, (int)Math.Floor(resultGiB * 1024d));
    }
}
