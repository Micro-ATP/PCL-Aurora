using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class ModrinthModpackImportServiceTests : IDisposable
{
    private static readonly Uri PackageUri = new("https://cdn.modrinth.com/test/test.mrpack");
    private static readonly Uri ModUri = new("https://cdn.modrinth.com/test/mod.jar");
    private static readonly Uri OptionalUri = new("https://cdn.modrinth.com/test/optional.jar");
    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"pcl-aurora-mrpack-{Guid.NewGuid():N}");

    [Fact]
    public async Task ImportAsync_DownloadsClientFilesAndAppliesOverridesTransactionally()
    {
        Directory.CreateDirectory(rootDirectory);
        var mod = "verified mod"u8.ToArray();
        var package = CreatePackage(
            [CreateFile("mods/mod.jar", ModUri, mod)],
            new Dictionary<string, byte[]> { ["overrides/config/example.txt"] = "configured"u8.ToArray() });
        using var client = CreateClient(package, (ModUri, mod));
        var service = new ModrinthModpackImportService(new MinecraftDownloadExecutor(client));

        var result = await service.ImportAsync(CreateProject(), CreateVersion(package), rootDirectory, "Example Pack");

        Assert.Equal(Path.Combine(rootDirectory, "Example Pack"), result.TargetDirectory);
        Assert.Equal("1.21.1", result.MinecraftVersion);
        Assert.Equal(MinecraftLoaderKind.Fabric, result.LoaderKind);
        Assert.Equal("0.16.10", result.LoaderVersion);
        Assert.Equal(1, result.DownloadedFileCount);
        Assert.Equal(1, result.OverrideFileCount);
        Assert.Equal(mod, await File.ReadAllBytesAsync(Path.Combine(result.TargetDirectory, "mods", "mod.jar")));
        Assert.Equal("configured", await File.ReadAllTextAsync(Path.Combine(result.TargetDirectory, "config", "example.txt")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(rootDirectory), path => path.EndsWith(".partial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_SkipsUnsupportedAndOptionalClientFilesWhenRequested()
    {
        Directory.CreateDirectory(rootDirectory);
        var mod = "required"u8.ToArray();
        var optional = "optional"u8.ToArray();
        var package = CreatePackage(
        [
            CreateFile("mods/required.jar", ModUri, mod),
            CreateFile("mods/optional.jar", OptionalUri, optional, "optional"),
            CreateFile("mods/server.jar", new Uri("https://cdn.modrinth.com/test/server.jar"), optional, "unsupported"),
        ]);
        using var client = CreateClient(package, (ModUri, mod));
        var service = new ModrinthModpackImportService(new MinecraftDownloadExecutor(client));

        var result = await service.ImportAsync(
            CreateProject(),
            CreateVersion(package),
            rootDirectory,
            "Required Only",
            includeOptionalClientFiles: false);

        Assert.Equal(1, result.DownloadedFileCount);
        Assert.True(File.Exists(Path.Combine(result.TargetDirectory, "mods", "required.jar")));
        Assert.False(File.Exists(Path.Combine(result.TargetDirectory, "mods", "optional.jar")));
        Assert.False(File.Exists(Path.Combine(result.TargetDirectory, "mods", "server.jar")));
    }

    [Fact]
    public async Task ImportAsync_RejectsPathTraversalWithoutPublishingDirectory()
    {
        Directory.CreateDirectory(rootDirectory);
        var mod = "outside"u8.ToArray();
        var package = CreatePackage([CreateFile("../outside.jar", ModUri, mod)]);
        using var client = CreateClient(package);
        var service = new ModrinthModpackImportService(new MinecraftDownloadExecutor(client));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ImportAsync(CreateProject(), CreateVersion(package), rootDirectory, "Unsafe Pack"));

        Assert.Contains("不安全", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(rootDirectory, "outside.jar")));
        Assert.False(Directory.Exists(Path.Combine(rootDirectory, "Unsafe Pack")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(rootDirectory));
    }

    [Fact]
    public async Task ImportAsync_RejectsNonHttpsDownloadBeforeResourceRequest()
    {
        Directory.CreateDirectory(rootDirectory);
        var mod = "insecure"u8.ToArray();
        var package = CreatePackage([CreateFile("mods/insecure.jar", new Uri("http://example.invalid/insecure.jar"), mod)]);
        var handler = new StaticHandler(new Dictionary<Uri, byte[]> { [PackageUri] = package });
        using var client = new HttpClient(handler);
        var service = new ModrinthModpackImportService(new MinecraftDownloadExecutor(client));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ImportAsync(CreateProject(), CreateVersion(package), rootDirectory, "Unsafe Source"));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
        Assert.Equal([PackageUri], handler.Requests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(rootDirectory));
    }

    [Fact]
    public async Task ImportAsync_RejectsOverridePathTraversalWithoutPublishingDirectory()
    {
        Directory.CreateDirectory(rootDirectory);
        var package = CreatePackage(
            [],
            new Dictionary<string, byte[]> { ["overrides/../../outside.txt"] = "outside"u8.ToArray() });
        using var client = CreateClient(package);
        var service = new ModrinthModpackImportService(new MinecraftDownloadExecutor(client));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ImportAsync(CreateProject(), CreateVersion(package), rootDirectory, "Unsafe Override"));

        Assert.Contains("覆盖文件路径", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(rootDirectory, "outside.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(rootDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static CommunityResourceProject CreateProject() =>
        new(
            "pack-project", "example-pack", "Example Pack", "", "Author", CommunityResourceType.ModPack,
            new Uri("https://modrinth.com/modpack/example-pack"), null, 0, 0, null, null, [], ["1.21.1"]);

    private static CommunityResourceVersion CreateVersion(byte[] package) =>
        new(
            "pack-version", "pack-project", "Example Pack", "1.0.0", CommunityResourceVersionChannel.Release,
            DateTimeOffset.UtcNow, 0, ["1.21.1"], ["fabric"],
            [new("example.mrpack", PackageUri, Sha1(package), package.Length, true)], []);

    private static object CreateFile(string path, Uri uri, byte[] content, string client = "required") => new
    {
        path,
        hashes = new { sha1 = Sha1(content) },
        env = new { client, server = "required" },
        downloads = new[] { uri.AbsoluteUri },
        fileSize = content.Length,
    };

    private static byte[] CreatePackage(
        IReadOnlyList<object> files,
        IReadOnlyDictionary<string, byte[]>? archiveFiles = null)
    {
        var index = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            game = "minecraft",
            versionId = "1.0.0",
            name = "Example Pack",
            files,
            dependencies = new Dictionary<string, string>
            {
                ["minecraft"] = "1.21.1",
                ["fabric-loader"] = "0.16.10",
            },
        });
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "modrinth.index.json", index);
            foreach (var file in archiveFiles ?? new Dictionary<string, byte[]>())
            {
                WriteEntry(archive, file.Key, file.Value);
            }
        }

        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static HttpClient CreateClient(byte[] package, params (Uri Uri, byte[] Content)[] files)
    {
        var responses = files.ToDictionary(file => file.Uri, file => file.Content);
        responses.Add(PackageUri, package);
        return new HttpClient(new StaticHandler(responses));
    }

    private static string Sha1(byte[] content) => Convert.ToHexString(SHA1.HashData(content));

    private sealed class StaticHandler(IReadOnlyDictionary<Uri, byte[]> responses) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                Requests.Add(request.RequestUri);
            }

            if (request.RequestUri is null || !responses.TryGetValue(request.RequestUri, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            });
        }
    }
}
