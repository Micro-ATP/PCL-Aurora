using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

var services = new ServiceCollection();
services.AddSingleton<IPlatformInfo, MacOSPlatformInfo>();
services.AddSingleton<IPlatformPaths, MacOSPlatformPaths>();
services.AddSingleton<ISystemMemoryInfo, MacOSSystemMemoryInfo>();
services.AddSingleton<IJavaLocator, MacOSJavaLocator>();
services.AddSingleton<IMinecraftInstanceLocator, MacOSMinecraftInstanceLocator>();
services.AddSingleton<IMinecraftRootDirectoryProvider, MacOSMinecraftRootDirectoryProvider>();
services.AddSingleton<IMinecraftVersionMetadataReader, MacOSMinecraftVersionMetadataReader>();
services.AddSingleton<IOpenPathService, MacOSOpenPathService>();
services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
services.AddSingleton<IInstanceCatalogService, InstanceCatalogService>();
services.AddSingleton<ILaunchReadinessService, LaunchReadinessService>();
services.AddSingleton<IMinecraftVersionPreparationService, MinecraftVersionPreparationService>();
services.AddSingleton<IMinecraftAssetPreparationService, MinecraftAssetPreparationService>();
services.AddSingleton<IMinecraftLaunchPreparationService, MinecraftLaunchPreparationService>();
services.AddSingleton<IMinecraftAssetIndexReader, MacOSMinecraftAssetIndexReader>();
services.AddSingleton<IAssetMapper, MinecraftAssetMapper>();
services.AddSingleton<HttpClient>();
services.AddSingleton(new MicrosoftAuthenticationConfiguration(Environment.GetEnvironmentVariable("PCL_AURORA_MS_CLIENT_ID")));
services.AddSingleton<IMicrosoftAccountAuthenticationService, MicrosoftAccountAuthenticationService>();
services.AddSingleton<ISecureSecretStore, MacOSKeychainSecretStore>();
services.AddSingleton<IMicrosoftAccountSessionService, MicrosoftAccountSessionService>();
services.AddSingleton<ILauncherPreferencesStore, JsonLauncherPreferencesStore>();
services.AddSingleton<ILauncherPreferencesService, LauncherPreferencesService>();
services.AddSingleton<IMinecraftDownloadExecutor, MinecraftDownloadExecutor>();
services.AddSingleton<IMinecraftInstanceInstallationService, MinecraftInstanceInstallationService>();
services.AddSingleton<IMinecraftVersionCatalogService, MinecraftVersionCatalogService>();
services.AddSingleton<IMinecraftLoaderCatalogService, MinecraftLoaderCatalogService>();
services.AddSingleton<IMinecraftOfficialLoaderCatalogService, MinecraftOfficialLoaderCatalogService>();
services.AddSingleton<IMinecraftLoaderInstallerProcessRunner, MinecraftLoaderInstallerProcessRunner>();
services.AddSingleton<IMinecraftLoaderInstallerService, MinecraftLoaderInstallerService>();
services.AddSingleton<IMinecraftVersionProvisioningService, MinecraftVersionProvisioningService>();
services.AddSingleton<IMinecraftDirectoryService, MinecraftDirectoryService>();
services.AddSingleton<INativeLibraryPreparer, MinecraftNativeLibraryPreparer>();
services.AddSingleton<IGameProcessRunner, MinecraftGameProcessRunner>();
services.AddSingleton<IMinecraftGameLaunchService, MinecraftGameLaunchService>();

await using var provider = services.BuildServiceProvider();
var diagnosticsService = provider.GetRequiredService<ISystemDiagnosticsService>();
var instanceCatalogService = provider.GetRequiredService<IInstanceCatalogService>();
var launchReadinessService = provider.GetRequiredService<ILaunchReadinessService>();
var versionPreparationService = provider.GetRequiredService<IMinecraftVersionPreparationService>();
var launchPreparationService = provider.GetRequiredService<IMinecraftLaunchPreparationService>();
var gameLaunchService = provider.GetRequiredService<IMinecraftGameLaunchService>();
var installationService = provider.GetRequiredService<IMinecraftInstanceInstallationService>();
var versionCatalogService = provider.GetRequiredService<IMinecraftVersionCatalogService>();
var loaderCatalogService = provider.GetRequiredService<IMinecraftLoaderCatalogService>();
var officialLoaderCatalogService = provider.GetRequiredService<IMinecraftOfficialLoaderCatalogService>();
var loaderInstallerService = provider.GetRequiredService<IMinecraftLoaderInstallerService>();
var minecraftDirectoryService = provider.GetRequiredService<IMinecraftDirectoryService>();
var versionProvisioningService = provider.GetRequiredService<IMinecraftVersionProvisioningService>();
var preferencesService = provider.GetRequiredService<ILauncherPreferencesService>();
await preferencesService.LoadAsync();

var command = args.Length == 0 ? "help" : string.Join(' ', args);
switch (command)
{
    case "info":
    {
        var diagnostics = await diagnosticsService.GetAsync();
        Console.WriteLine($"系统：{diagnostics.Platform.OperatingSystem} ({diagnostics.Platform.Version})");
        Console.WriteLine($"架构：{diagnostics.Platform.Architecture}");
        Console.WriteLine($"运行时：{diagnostics.Platform.RuntimeVersion}");
        Console.WriteLine($"数据目录：{diagnostics.Paths.ApplicationDataDirectory}");
        Console.WriteLine($"缓存目录：{diagnostics.Paths.CacheDirectory}");
        break;
    }
    case "java list":
    {
        var diagnostics = await diagnosticsService.GetAsync();
        if (diagnostics.JavaInstallations.Count == 0)
        {
            Console.WriteLine("未发现可用 Java。请安装 Java，或设置 JAVA_HOME。");
            return 1;
        }

        foreach (var java in diagnostics.JavaInstallations)
        {
            Console.WriteLine($"{java.Version ?? "未知版本"} | {java.Architecture} | {java.Vendor} | {(java.IsCompatible ? "兼容" : "不兼容")}");
            Console.WriteLine($"  {java.ExecutablePath} ({java.Source})");
        }

        break;
    }
    case "doctor":
    {
        var diagnostics = await diagnosticsService.GetAsync();
        Console.WriteLine($"平台诊断：{diagnostics.Platform.OperatingSystem} / {diagnostics.Platform.Architecture}");
        Console.WriteLine($"Java：发现 {diagnostics.JavaInstallations.Count} 个可执行文件，其中 {diagnostics.JavaInstallations.Count(java => java.IsCompatible)} 个与当前系统架构兼容。");
        return diagnostics.JavaInstallations.Any(java => java.IsCompatible) ? 0 : 1;
    }
    case "instances list":
    {
        var instances = await instanceCatalogService.GetAllAsync();
        if (instances.Count == 0)
        {
            Console.WriteLine("未在 macOS 默认 Minecraft 目录中发现实例。");
            return 0;
        }

        foreach (var instance in instances)
        {
            Console.WriteLine($"{instance.Name} | {instance.VersionDisplay} | {instance.LoaderDisplay} | {instance.Type ?? "未知类型"} | {instance.Status}");
            Console.WriteLine($"  {instance.DirectoryPath}");
        }

        break;
    }
    case "versions list":
    {
        var catalog = await versionCatalogService.FetchAsync();
        if (!catalog.IsSuccess || catalog.Catalog is null)
        {
            Console.WriteLine(string.Join(Environment.NewLine, catalog.Errors));
            return 1;
        }

        Console.WriteLine($"最新正式版：{catalog.Catalog.LatestRelease ?? "未知"}；最新快照：{catalog.Catalog.LatestSnapshot ?? "未知"}");
        foreach (var version in catalog.Catalog.Versions.Take(20))
        {
            Console.WriteLine($"{version.Id} | {version.Type} | {version.ReleaseTime:yyyy-MM-dd}");
        }

        break;
    }
    case "loaders inspect":
    case var value when value.StartsWith("loaders inspect ", StringComparison.Ordinal):
    {
        var catalogPath = args.ElementAtOrDefault(2);
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            Console.WriteLine("请提供本地加载器目录 JSON 文件路径。不会访问网络或执行安装器。");
            return 64;
        }

        var catalog = await loaderCatalogService.ReadAsync(catalogPath);
        if (!catalog.IsSuccess || catalog.Catalog is null)
        {
            Console.WriteLine("加载器目录检查未通过：");
            foreach (var error in catalog.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        Console.WriteLine($"来源：{catalog.Catalog.SourceName}；共 {catalog.Catalog.Entries.Count} 个加载器版本。 ");
        foreach (var entry in catalog.Catalog.Entries)
        {
            Console.WriteLine($"{entry.Kind} | Minecraft {entry.MinecraftVersion} | {entry.Version} | {entry.Channel}");
        }

        Console.WriteLine("仅完成本地目录读取和兼容性建模；未访问网络、下载或执行安装器。");
        break;
    }
    case "loaders refresh":
    case var value when value.StartsWith("loaders refresh ", StringComparison.Ordinal):
    {
        var minecraftVersion = args.ElementAtOrDefault(2);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            Console.WriteLine("请提供 Minecraft 版本。此命令会访问 Forge、NeoForge、Fabric 官方目录和 PCL 使用的 OptiFine 公开目录，但不会下载或执行安装器。");
            return 64;
        }

        var catalog = await officialLoaderCatalogService.FetchAsync(minecraftVersion);
        if (catalog.Catalog is null)
        {
            Console.WriteLine("官方加载器目录检查未通过：");
            foreach (var error in catalog.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        Console.WriteLine($"来源：{catalog.Catalog.SourceName}；Minecraft {minecraftVersion} 共 {catalog.Catalog.Entries.Count} 个加载器版本。 ");
        foreach (var entry in catalog.Catalog.Entries)
        {
            Console.WriteLine($"{entry.Kind} | {entry.Version} | {entry.Channel}");
        }

        foreach (var warning in catalog.Errors)
        {
            Console.WriteLine($"警告：{warning}");
        }

        Console.WriteLine("仅完成公开目录读取和兼容性建模；未下载或执行安装器。 ");
        break;
    }
    case "loaders install":
    case var value when value.StartsWith("loaders install ", StringComparison.Ordinal):
    {
        if (args.Length != 6 || !string.Equals(args[5], "--confirm", StringComparison.Ordinal))
        {
            Console.WriteLine("用法：loaders install <Forge|NeoForge|Fabric|OptiFine> <Minecraft 版本> <加载器版本> --confirm");
            Console.WriteLine("只有带 --confirm 的命令才会访问公开地址、下载加载器；旧版 OptiFine 会创建继承版本，其余加载器会执行安装器。");
            return 64;
        }

        if (!Enum.TryParse<MinecraftLoaderKind>(args[2], ignoreCase: true, out var kind) ||
            !Enum.IsDefined(kind))
        {
            Console.WriteLine("加载器类型必须是 Forge、NeoForge、Fabric 或 OptiFine。未访问网络。 ");
            return 64;
        }

        var minecraftVersion = args[3];
        var loaderVersion = args[4];
        var catalog = await officialLoaderCatalogService.FetchAsync(minecraftVersion);
        var loader = catalog.Catalog?.Entries.FirstOrDefault(entry =>
            entry.Kind == kind &&
            string.Equals(entry.Version, loaderVersion, StringComparison.OrdinalIgnoreCase));
        if (loader is null)
        {
            Console.WriteLine("未在官方目录中找到指定的兼容加载器；不会下载或执行安装器。 ");
            foreach (var error in catalog.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        var diagnostics = await diagnosticsService.GetAsync();
        var java = diagnostics.JavaInstallations.FirstOrDefault(candidate => candidate.IsCompatible);
        var minecraftRoot = minecraftDirectoryService.GetRootDirectory();
        var plan = await loaderInstallerService.PrepareAsync(loader, minecraftRoot, java);
        if (!plan.CanInstall)
        {
            Console.WriteLine("加载器安装计划未通过：");
            foreach (var reason in plan.BlockingReasons)
            {
                Console.WriteLine($"- {reason}");
            }

            return 1;
        }

        Console.WriteLine(MinecraftLoaderInstallerPlanBuilder.IsLegacyOptiFine(loader)
            ? $"正在下载并创建旧版 OptiFine {loader.Version} 继承版本…"
            : $"正在下载并安装 {loader.Kind} {loader.Version}…");
        var result = await loaderInstallerService.InstallAsync(plan, minecraftRoot, hasExplicitUserConfirmation: true);
        foreach (var output in result.Output)
        {
            Console.WriteLine(output.Text);
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        Console.WriteLine("加载器安装完成。请刷新本地实例列表以检查新增版本元数据。 ");
        break;
    }
    case var value when value.StartsWith("install create ", StringComparison.Ordinal):
    {
        var requestedId = args.ElementAtOrDefault(2);
        if (string.IsNullOrWhiteSpace(requestedId))
        {
            Console.WriteLine("请提供要创建的版本 ID。 ");
            return 64;
        }

        var catalog = await versionCatalogService.FetchAsync();
        var version = catalog.Catalog?.Versions.FirstOrDefault(item => string.Equals(item.Id, requestedId, StringComparison.Ordinal));
        if (!catalog.IsSuccess || version is null)
        {
            Console.WriteLine($"未在官方版本清单中找到 {requestedId}；不会创建实例。 ");
            return 1;
        }

        var instance = await versionProvisioningService.ProvisionAsync(version);
        Console.WriteLine($"已创建本地实例元数据：{instance.DirectoryPath}");
        Console.WriteLine("请继续运行 install local 以显式下载游戏文件。 ");
        break;
    }
    case "launch check":
    {
        var instancesTask = instanceCatalogService.GetAllAsync();
        var diagnosticsTask = diagnosticsService.GetAsync();
        await Task.WhenAll(instancesTask, diagnosticsTask);
        var instance = (await instancesTask).FirstOrDefault(candidate => candidate.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid);
        var java = (await diagnosticsTask).JavaInstallations.FirstOrDefault(candidate => candidate.IsCompatible);
        var readiness = launchReadinessService.Evaluate(instance, account: null, java);

        if (readiness.CanLaunch)
        {
            Console.WriteLine("启动前检查通过。游戏进程启动器尚未迁移。");
            break;
        }

        Console.WriteLine("启动前检查未通过：");
        foreach (var reason in readiness.BlockingReasons)
        {
            Console.WriteLine($"- {reason}");
        }

        return 1;
    }
    case "install local":
    {
        var instance = (await instanceCatalogService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Status == MinecraftInstanceStatus.Valid);
        if (instance is null)
        {
            Console.WriteLine("未发现可安装的本地实例；不会发起下载。\n");
            return 1;
        }

        var progress = new Progress<MinecraftInstallationProgress>(update =>
            Console.WriteLine($"[{update.CompletedStages}/{update.TotalStages}] {update.Description}"));
        await installationService.InstallAsync(instance, progress);
        Console.WriteLine("安装下载完成。资源映射将在下一次显式启动时准备。");
        break;
    }
    case "launch arguments":
    {
        var instance = (await instanceCatalogService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid);
        if (instance is null)
        {
            Console.WriteLine("未发现可读取版本元数据的本地实例。不会启动游戏进程。");
            return 1;
        }

        var preparation = await launchPreparationService.PrepareAsync(instance, account: null);
        if (!preparation.ClasspathInspection.IsReady)
        {
            Console.WriteLine("类路径尚未准备：");
            foreach (var reason in preparation.ClasspathInspection.BlockingReasons)
            {
                Console.WriteLine($"- {reason}");
            }

            foreach (var missingFile in preparation.ClasspathInspection.MissingFiles)
            {
                Console.WriteLine($"- 缺少文件：{missingFile}");
            }
        }

        if (!preparation.ArgumentPreparation.IsReady)
        {
            Console.WriteLine("启动参数尚未准备：");
            foreach (var reason in preparation.ArgumentPreparation.BlockingReasons)
            {
                Console.WriteLine($"- {reason}");
            }

            return 1;
        }

        var arguments = preparation.ArgumentPreparation.Arguments!;
        Console.WriteLine($"主类：{arguments.MainClass}");
        Console.WriteLine($"JVM 参数：{arguments.JvmArguments.Count} 项；游戏参数：{arguments.GameArguments.Count} 项。");
        Console.WriteLine("仅完成参数准备；未启动 Java 或 Minecraft 进程。");
        break;
    }
    case "launch run":
    case var value when value.StartsWith("launch run ", StringComparison.Ordinal):
    {
        if (args.Length > 4 || (args.Length == 4 && !string.Equals(args[3], "--acknowledge-offline", StringComparison.Ordinal)))
        {
            Console.WriteLine("用法：launch run [离线用户名] [--acknowledge-offline]");
            return 64;
        }

        MinecraftAccount? account = null;
        if (args.Length >= 3 && !OfflineAccount.TryCreate(args[2], out account))
        {
            Console.WriteLine("离线用户名需为 3–16 位英文字母、数字或下划线。");
            return 64;
        }

        var instancesTask = instanceCatalogService.GetAllAsync();
        var diagnosticsTask = diagnosticsService.GetAsync();
        await Task.WhenAll(instancesTask, diagnosticsTask);
        var instance = (await instancesTask).FirstOrDefault(candidate => candidate.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid);
        var java = (await diagnosticsTask).JavaInstallations.FirstOrDefault(candidate => candidate.IsCompatible);
        var preparation = await gameLaunchService.PrepareAsync(
            instance,
            account,
            java,
            hasAcknowledgedAccountGuidance: args.Length == 4);
        if (!preparation.CanLaunch)
        {
            Console.WriteLine("游戏启动被阻断：");
            foreach (var reason in preparation.BlockingReasons)
            {
                Console.WriteLine($"- {reason}");
            }

            Console.WriteLine("不会启动游戏进程。");
            return 1;
        }

        var session = await gameLaunchService.LaunchAsync(preparation);
        Console.WriteLine($"已启动游戏进程：{session.ProcessId}");
        await foreach (var output in session.Output.ReadAllAsync())
        {
            Console.WriteLine(output.Text);
        }

        return await session.ExitCode;
    }
    case "versions inspect":
    {
        var instance = (await instanceCatalogService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid);
        if (instance is null)
        {
            Console.WriteLine("未发现可读取版本元数据的本地实例。不会创建目录或下载文件。");
            return 1;
        }

        var preparation = await versionPreparationService.PrepareAsync(instance);
        if (!preparation.Inspection.IsSuccess || preparation.Inspection.EffectiveMetadata is null)
        {
            Console.WriteLine("版本元数据检查未通过：");
            foreach (var error in preparation.Inspection.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return 1;
        }

        var metadata = preparation.Inspection.EffectiveMetadata;
        Console.WriteLine($"版本：{metadata.Id} | {metadata.Type ?? "未知类型"}");
        Console.WriteLine($"继承链：{string.Join(" -> ", preparation.Inspection.InheritanceChain.Select(item => item.Id))}");
        if (preparation.DownloadPlan.IsReady)
        {
            Console.WriteLine($"已生成 {preparation.DownloadPlan.Artifacts.Count} 个只读下载计划项；未下载文件。");
            foreach (var artifact in preparation.DownloadPlan.Artifacts)
            {
                Console.WriteLine($"- {artifact.Description}：{artifact.RelativePath}");
            }

            break;
        }

        Console.WriteLine("下载计划不完整：");
        foreach (var reason in preparation.DownloadPlan.BlockingReasons)
        {
            Console.WriteLine($"- {reason}");
        }

        return 1;
    }
    default:
        Console.WriteLine("PCL Aurora 诊断工具");
        Console.WriteLine("用法：info | java list | instances list | versions list | versions inspect | loaders inspect <本地目录.json> | loaders refresh <Minecraft 版本> | loaders install <Forge|NeoForge|Fabric> <Minecraft 版本> <加载器版本> --confirm | install create <版本 ID> | install local | launch check | launch arguments | launch run [离线用户名] [--acknowledge-offline] | doctor");
        return command == "help" ? 0 : 64;
}

return 0;
