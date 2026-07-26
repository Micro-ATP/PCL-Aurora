using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftLoaderInstallerService(
    HttpClient httpClient,
    IMinecraftDownloadExecutor downloadExecutor,
    IMinecraftLoaderInstallerProcessRunner processRunner) : IMinecraftLoaderInstallerService
{
    private static readonly Uri FabricInstallerCatalogUri = new("https://meta.fabricmc.net/v2/versions/installer");

    public async Task<MinecraftLoaderInstallerPlan> PrepareAsync(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        JavaInstallation? java,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        Uri? fabricInstallerUri = null;
        if (loader.Kind == MinecraftLoaderKind.Fabric)
        {
            try
            {
                using var response = await httpClient.GetAsync(FabricInstallerCatalogUri, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                fabricInstallerUri = MinecraftFabricInstallerMetadataParser.ParseLatestStableInstallerUri(content);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException)
            {
                return new(loader, null, null, [$"无法获取 Fabric 官方安装器目录：{exception.Message}"]);
            }
        }

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, minecraftRootDirectory, java, fabricInstallerUri);
        if (!plan.CanInstall || plan.InstallerArtifact?.Sha1Url is not { } sha1Url)
        {
            return plan;
        }

        try
        {
            using var response = await httpClient.GetAsync(sha1Url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var sha1 = ParseSha1(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return sha1 is null
                ? plan with { InstallerArtifact = null, ProcessRequest = null, BlockingReasons = ["官方安装器校验文件无效；未下载或执行安装器。"] }
                : plan with { InstallerArtifact = plan.InstallerArtifact with { Sha1 = sha1 } };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return plan with
            {
                InstallerArtifact = null,
                ProcessRequest = null,
                BlockingReasons = [$"无法获取官方安装器 SHA-1 校验文件：{exception.Message}"],
            };
        }
    }

    public async Task<MinecraftLoaderInstallerExecutionResult> InstallAsync(
        MinecraftLoaderInstallerPlan plan,
        string minecraftRootDirectory,
        bool hasExplicitUserConfirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!hasExplicitUserConfirmation)
        {
            return new(null, [], ["加载器安装需要用户明确确认；未下载或执行安装器。"]);
        }

        if (!plan.CanInstall)
        {
            return new(null, [], plan.BlockingReasons.Count == 0 ? ["加载器安装计划不完整。"] : plan.BlockingReasons);
        }

        try
        {
            var downloadPlan = new MinecraftDownloadPlan(
                $"loader-installer:{plan.Loader.Kind}:{plan.Loader.Version}",
                [plan.InstallerArtifact!],
                []);
            await downloadExecutor.ExecuteAsync(downloadPlan, minecraftRootDirectory, cancellationToken).ConfigureAwait(false);
            return await processRunner.ExecuteAsync(plan.ProcessRequest!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or InvalidDataException or InvalidOperationException)
        {
            return new(null, [], [$"加载器安装未完成：{exception.Message}"]);
        }
    }

    private static string? ParseSha1(string content)
    {
        var value = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return value is { Length: 40 } && value.All(Uri.IsHexDigit) ? value : null;
    }
}
