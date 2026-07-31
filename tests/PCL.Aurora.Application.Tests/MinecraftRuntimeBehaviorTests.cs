using System.Text.Json.Nodes;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftRuntimeBehaviorTests
{
    [Fact]
    public async Task OptionsUpdater_PreservesExistingOptionsAndUsesLegacyLanguageCase()
    {
        var directory = CreateTemporaryDirectory();
        await File.WriteAllLinesAsync(Path.Combine(directory, "options.txt"), ["music:0.5", "lang:none"]);

        await PclCeMinecraftOptionsUpdater.UpdateLanguageAsync(
            directory,
            new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "zh-CN");

        var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "options.txt"));
        Assert.Contains("music:0.5", lines);
        Assert.Contains("lang:zh_CN", lines);
        Assert.Contains("forceUnicodeFont:true", lines);
    }

    [Fact]
    public void AuthlibFixer_UpdatesOnlyTheKnownMinecraft1165Artifact()
    {
        const string json = """
        {
          "libraries": [
            {
              "name": "com.mojang:authlib:2.1.28",
              "downloads": { "artifact": {
                "path": "com/mojang/authlib/2.1.28/authlib-2.1.28.jar",
                "url": "https://libraries.minecraft.net/com/mojang/authlib/2.1.28/authlib-2.1.28.jar",
                "sha1": "ad54da276bf59983d02d5ed16fc14541354c71fd",
                "size": 76328
              }}
            }
          ]
        }
        """;

        var fixedJson = PclCeAuthlibMetadataFixer.Apply(json);
        var artifact = JsonNode.Parse(fixedJson)!["libraries"]![0]!;

        Assert.Equal("com.mojang:authlib:2.3.31", artifact["name"]!.GetValue<string>());
        Assert.Contains("2.3.31/authlib-2.3.31.jar", artifact["downloads"]!["artifact"]!["path"]!.GetValue<string>());
        Assert.Equal(87662, artifact["downloads"]!["artifact"]!["size"]!.GetValue<long>());
    }

    [Fact]
    public async Task LaunchPatchService_ExtractsAndInjectsRequiredAgents()
    {
        var cache = CreateTemporaryDirectory();
        var service = new MinecraftLaunchPatchService(new FixedPlatformPaths(cache));
        var releaseTime = new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var instance = new MinecraftInstance(
            "old",
            Path.Combine(cache, ".minecraft", "versions", "old"),
            "old",
            "old_alpha",
            releaseTime,
            MinecraftInstanceStatus.Valid);
        var metadata = new MinecraftVersionMetadata(
            "old",
            null,
            "old_alpha",
            releaseTime,
            null,
            null,
            Libraries: [new MinecraftVersionLibrary("org.lwjgl:lwjgl:3.4.1", null, null, false)]);
        var java = new JavaInstallation("/usr/bin/java", "8", 8, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);
        var request = new MinecraftGameLaunchRequest(
            java.ExecutablePath,
            cache,
            ["-Xmx1G", "example.Main"],
            new Dictionary<string, string>(),
            MainClassArgumentIndex: 1);

        var preparation = await service.PrepareAsync(
            instance,
            metadata,
            java,
            MinecraftLaunchOptions.Default,
            request);

        Assert.True(preparation.IsReady);
        Assert.Contains(preparation.Request!.ArgumentList, argument => argument.Contains("legacyfix.jar", StringComparison.Ordinal));
        Assert.Contains(preparation.Request.ArgumentList, argument => argument.Contains("lwjgl-unsafe-agent.jar", StringComparison.Ordinal));
        Assert.Contains("-Djava.util.Arrays.useLegacyMergeSort=true", preparation.Request.ArgumentList);
        Assert.True(File.Exists(Path.Combine(cache, "LaunchPatches", "legacyfix.jar")));
        Assert.True(File.Exists(Path.Combine(cache, "LaunchPatches", "lwjgl-unsafe-agent.jar")));
    }

    [Fact]
    public void CrashAnalyzer_RecognizesMemoryAndJavaFailures()
    {
        var memory = PclCeMinecraftCrashAnalyzer.Analyze(1, ["java.lang.OutOfMemoryError: Java heap space"]);
        var java = PclCeMinecraftCrashAnalyzer.Analyze(1, ["java.lang.UnsupportedClassVersionError: class file version 65.0"]);

        Assert.Contains("内存", memory.Summary);
        Assert.Contains("Java", java.Summary);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcl-aurora-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedPlatformPaths(string cacheDirectory) : IPlatformPaths
    {
        public PlatformPaths Get() => new(cacheDirectory, cacheDirectory);
    }
}
