using System.IO.Compression;
using System.Text.Json;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftInstanceManagementServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pcl-aurora-instance-manage-{Guid.NewGuid():N}");
    private readonly MinecraftInstanceManagementService service = new();

    [Fact]
    public async Task InspectAsync_UsesResolvedIsolatedGameDirectoryAndCountsContent()
    {
        var instance = await CreateInstanceAsync("Fabric 1.20.1");
        Directory.CreateDirectory(Path.Combine(instance.DirectoryPath, "mods"));
        await File.WriteAllTextAsync(Path.Combine(instance.DirectoryPath, "mods", "example.jar"), "mod");
        Directory.CreateDirectory(Path.Combine(instance.DirectoryPath, "saves", "World"));
        await File.WriteAllTextAsync(Path.Combine(instance.DirectoryPath, "saves", "World", "level.dat"), "world");

        var snapshot = await service.InspectAsync(instance, MinecraftInstanceIsolationMode.All);

        Assert.Equal(instance.DirectoryPath, snapshot.GameDirectory);
        Assert.Equal(1, snapshot.GetCount(MinecraftInstanceContentKind.Mod));
        Assert.Equal(1, snapshot.GetCount(MinecraftInstanceContentKind.Save));
    }

    [Fact]
    public async Task InspectAsync_InstanceIsolationOverrideWinsOverGlobalMode()
    {
        var instance = await CreateInstanceAsync("1.20.4");
        await service.SaveProfileAsync(
            instance,
            new MinecraftInstanceProfile(IsolationMode: MinecraftInstanceIsolationMode.Disabled));

        var snapshot = await service.InspectAsync(instance, MinecraftInstanceIsolationMode.All);

        Assert.Equal(MinecraftInstanceIsolationMode.Disabled, snapshot.EffectiveIsolationMode);
        Assert.Equal(Path.Combine(root, "minecraft"), snapshot.GameDirectory);
    }

    [Fact]
    public async Task ImportToggleAndDelete_ModUsesAtomicNamesAndRejectsOverwrite()
    {
        var instance = await CreateInstanceAsync("1.20.1");
        var source = Path.Combine(root, "example.jar");
        await File.WriteAllTextAsync(source, "mod-content");

        var result = await service.ImportAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod,
            [source]);
        Assert.Equal(1, result.ImportedCount);
        await Assert.ThrowsAsync<IOException>(() => service.ImportAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod,
            [source]));

        await service.SetContentEnabledAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod,
            "example.jar",
            enabled: false);
        var disabled = Assert.Single(await service.GetContentAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod));
        Assert.False(disabled.IsEnabled);
        Assert.Equal("example.jar.disabled", disabled.RelativePath);

        await service.DeleteContentAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod,
            disabled.RelativePath);
        Assert.Empty(await service.GetContentAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            MinecraftInstanceContentKind.Mod));
    }

    [Fact]
    public async Task Servers_RoundTripMinecraftNbt()
    {
        var instance = await CreateInstanceAsync("1.21");
        MinecraftServerEntry[] expected =
        [
            new("本地服务器", "127.0.0.1:25565", AcceptTextures: true),
            new("示例", "mc.example.com", Hidden: true),
        ];

        await service.SaveServersAsync(instance, MinecraftInstanceIsolationMode.All, expected);
        var actual = await service.GetServersAsync(instance, MinecraftInstanceIsolationMode.All);

        Assert.Equal(expected, actual);
        Assert.True(File.Exists(Path.Combine(instance.DirectoryPath, "servers.dat")));
    }

    [Fact]
    public async Task CopyAndRename_RewriteDirectoryMetadataAndCoreNames()
    {
        var instance = await CreateInstanceAsync("Old");
        await File.WriteAllTextAsync(Path.Combine(instance.DirectoryPath, "Old.jar"), "jar");

        var copiedPath = await service.CopyAsync(instance, "Copy");
        Assert.True(File.Exists(Path.Combine(copiedPath, "Copy.json")));
        Assert.True(File.Exists(Path.Combine(copiedPath, "Copy.jar")));
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(copiedPath, "Copy.json"))))
        {
            Assert.Equal("Copy", document.RootElement.GetProperty("id").GetString());
        }

        var renamedPath = await service.RenameAsync(instance, "Renamed");
        Assert.False(Directory.Exists(instance.DirectoryPath));
        Assert.True(File.Exists(Path.Combine(renamedPath, "Renamed.json")));
        Assert.True(File.Exists(Path.Combine(renamedPath, "Renamed.jar")));
    }

    [Fact]
    public async Task ExportInstanceAsync_WritesVersionAndOptionalGameData()
    {
        var instance = await CreateInstanceAsync("1.19.4");
        Directory.CreateDirectory(Path.Combine(instance.DirectoryPath, "mods"));
        await File.WriteAllTextAsync(Path.Combine(instance.DirectoryPath, "mods", "a.jar"), "a");
        var archivePath = Path.Combine(root, "backup.zip");

        var result = await service.ExportInstanceAsync(
            instance,
            MinecraftInstanceIsolationMode.All,
            archivePath,
            includeGameData: true);

        Assert.True(result.FileCount >= 2);
        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "versions/1.19.4/1.19.4.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "versions/1.19.4/mods/a.jar");
    }

    private async Task<MinecraftInstance> CreateInstanceAsync(string name)
    {
        var directory = Path.Combine(root, "minecraft", "versions", name);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, name + ".json"),
            $$"""
              {
                "id": "{{name}}",
                "type": "release",
                "releaseTime": "2024-01-01T00:00:00Z"
              }
              """);
        return new(name, directory, name, "release", DateTimeOffset.UtcNow, MinecraftInstanceStatus.Valid);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
