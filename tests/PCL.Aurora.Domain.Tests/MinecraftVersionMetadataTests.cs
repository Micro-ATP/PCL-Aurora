using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftVersionMetadataTests
{
    [Fact]
    public void Parse_ReadsStandardVersionMetadataAndDownloadDescriptors()
    {
        var result = MinecraftVersionMetadataParser.Parse(
            """
            {
              "id": "1.21.4",
              "type": "release",
              "releaseTime": "2024-12-03T00:00:00Z",
              "downloads": { "client": { "url": "https://example.invalid/client.jar", "sha1": "client-sha", "size": 123 } },
              "assetIndex": { "id": "17", "url": "https://example.invalid/assets.json", "sha1": "assets-sha", "size": 456 }
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.21.4", result.Metadata!.Id);
        Assert.Equal("release", result.Metadata.Type);
        Assert.Equal(123, result.Metadata.ClientDownload!.Size);
        Assert.Equal("17", result.Metadata.AssetIndex!.Id);
    }

    [Fact]
    public void Resolve_InheritsMissingDownloadDescriptorsFromParent()
    {
        var child = new MinecraftVersionMetadata("fabric-1.21.4", "1.21.4", null, null, null,
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), null, null));
        var parent = new MinecraftVersionMetadata("1.21.4", null, "release", null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null), null);

        var inspection = MinecraftVersionMetadataResolver.Resolve([child, parent]);
        var plan = MinecraftDownloadPlanBuilder.Create(inspection.EffectiveMetadata);

        Assert.True(inspection.IsSuccess);
        Assert.Equal("fabric-1.21.4", inspection.EffectiveMetadata!.Id);
        Assert.NotNull(inspection.EffectiveMetadata.ClientDownload);
        Assert.NotNull(inspection.EffectiveMetadata.AssetIndex);
        Assert.True(plan.IsReady);
        Assert.Equal(2, plan.Artifacts.Count);
    }

    [Fact]
    public void Parse_RejectsInvalidJson()
    {
        var result = MinecraftVersionMetadataParser.Parse("{ invalid }");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Metadata);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Prepare_ReplacesModernArgumentsWithoutShellSplitting()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            null,
            null,
            new MinecraftLaunchMetadata(
                "net.minecraft.client.main.Main",
                ["-Djava.library.path=${natives_directory}", "-cp", "${classpath}"],
                ["--username", "${auth_player_name}", "--width", "${resolution_width}"],
                HasModernArguments: true,
                HasConditionalArguments: false,
                LegacyGameArguments: null));
        OfflineAccount.TryCreate("AuroraPlayer", out var account);
        var context = new MinecraftLaunchContext(
            "/libraries/a.jar:/libraries/b.jar",
            "/minecraft/natives folder",
            "/minecraft",
            "/minecraft/assets",
            "17",
            "PCL Aurora",
            "0.1.0",
            "1.21.4",
            "release",
            account,
            1280,
            720);

        var preparation = MinecraftLaunchArgumentBuilder.Prepare(metadata, context);

        Assert.True(preparation.IsReady);
        Assert.Equal("-Djava.library.path=/minecraft/natives folder", preparation.Arguments!.JvmArguments[0]);
        Assert.Equal("/libraries/a.jar:/libraries/b.jar", preparation.Arguments.JvmArguments[2]);
        Assert.Equal("AuroraPlayer", preparation.Arguments.GameArguments[1]);
    }

    [Fact]
    public void Prepare_BlocksConditionalArgumentsAndUnresolvedPlaceholders()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            null,
            null,
            new MinecraftLaunchMetadata(
                "net.minecraft.client.main.Main",
                ["-cp", "${classpath}"],
                [],
                HasModernArguments: true,
                HasConditionalArguments: true,
                LegacyGameArguments: null));

        var preparation = MinecraftLaunchArgumentBuilder.Prepare(metadata, MinecraftLaunchContext.CreateDefault("1.21.4"));

        Assert.False(preparation.IsReady);
        Assert.Contains(preparation.BlockingReasons, reason => reason.Contains("条件启动参数", StringComparison.Ordinal));
        Assert.Contains(preparation.BlockingReasons, reason => reason.Contains("${classpath}", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReadsModernArgumentsAndMarksConditionalEntriesAsUnsupported()
    {
        var result = MinecraftVersionMetadataParser.Parse(
            """
            {
              "id": "1.21.4",
              "mainClass": "net.minecraft.client.main.Main",
              "arguments": {
                "jvm": ["-cp", "${classpath}", { "rules": [], "value": "-Dignored=true" }],
                "game": ["--username", "${auth_player_name}"]
              },
              "libraries": [
                {
                  "name": "com.example:demo:1.0",
                  "downloads": {
                    "artifact": {
                      "path": "com/example/demo/1.0/demo-1.0.jar",
                      "url": "https://example.invalid/demo-1.0.jar",
                      "sha1": "demo-sha",
                      "size": 123
                    }
                  }
                }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("net.minecraft.client.main.Main", result.Metadata!.Launch!.MainClass);
        Assert.Equal(["-cp", "${classpath}"], result.Metadata.Launch.JvmArguments);
        Assert.Equal(["--username", "${auth_player_name}"], result.Metadata.Launch.GameArguments);
        Assert.True(result.Metadata.Launch.HasConditionalArguments);
        var library = Assert.Single(result.Metadata.Libraries!);
        Assert.Equal("com/example/demo/1.0/demo-1.0.jar", library.ArtifactPath);
        Assert.Equal(123, library.Artifact!.Size);
    }

    [Fact]
    public void Parse_ReadsMacOSNativeClassifiers()
    {
        var result = MinecraftVersionMetadataParser.Parse(
            """
            {
              "id": "1.21.4",
              "libraries": [
                {
                  "name": "org.example:native:1.0",
                  "natives": { "osx": "natives-macos-${arch}" },
                  "downloads": {
                    "classifiers": {
                      "natives-macos-arm64": {
                        "path": "org/example/native/1.0/native-arm64.jar",
                        "url": "https://example.invalid/native-arm64.jar",
                        "sha1": "native-sha",
                        "size": 456
                      }
                    }
                  }
                }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        var library = Assert.Single(result.Metadata!.Libraries!);
        Assert.Equal("natives-macos-${arch}", library.NativeClassifiers!["osx"]);
        var classifier = library.Classifiers!["natives-macos-arm64"];
        Assert.Equal("org/example/native/1.0/native-arm64.jar", classifier.Path);
        Assert.Equal(456, classifier.Download!.Size);
    }

    [Fact]
    public void CreateDownloadPlan_IncludesExplicitLibraryAndMacOSNativeArtifacts()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null),
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), null, null),
            null,
            [
                new MinecraftVersionLibrary(
                    "org.example:library:1.0",
                    "org/example/library/1.0/library-1.0.jar",
                    new MinecraftVersionDownload(new Uri("https://example.invalid/library.jar"), null, null),
                    HasConditionalRules: false),
                new MinecraftVersionLibrary(
                    "org.example:native:1.0",
                    null,
                    null,
                    HasConditionalRules: false,
                    NativeClassifiers: new Dictionary<string, string> { ["osx"] = "natives-macos-${arch}" },
                    Classifiers: new Dictionary<string, MinecraftVersionLibraryClassifier>
                    {
                        ["natives-macos-arm64"] = new(
                            "org/example/native/1.0/native-arm64.jar",
                            new MinecraftVersionDownload(new Uri("https://example.invalid/native.jar"), "native-sha", 123)),
                    }),
            ]);
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var plan = MinecraftDownloadPlanBuilder.Create(inspection, JavaArchitecture.Arm64);

        Assert.True(plan.IsReady);
        Assert.Equal(
            [
                "versions/1.21.4/1.21.4.jar",
                "assets/indexes/17.json",
                "libraries/org/example/library/1.0/library-1.0.jar",
                "libraries/org/example/native/1.0/native-arm64.jar",
            ],
            plan.Artifacts.Select(artifact => artifact.RelativePath));
    }

    [Fact]
    public void CreateDownloadPlan_BlocksUnsafeLibraryArtifactPath()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null),
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), null, null),
            null,
            [new MinecraftVersionLibrary(
                "org.example:unsafe:1.0",
                "../unsafe.jar",
                new MinecraftVersionDownload(new Uri("https://example.invalid/unsafe.jar"), null, null),
                HasConditionalRules: false)]);
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var plan = MinecraftDownloadPlanBuilder.Create(inspection, JavaArchitecture.Arm64);

        Assert.False(plan.IsReady);
        Assert.Contains(plan.BlockingReasons, reason => reason.Contains("路径无效", StringComparison.Ordinal));
    }
}
