using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

var services = new ServiceCollection();
services.AddSingleton<IPlatformInfo, MacOSPlatformInfo>();
services.AddSingleton<IPlatformPaths, MacOSPlatformPaths>();
services.AddSingleton<IJavaLocator, MacOSJavaLocator>();
services.AddSingleton<IMinecraftInstanceLocator, MacOSMinecraftInstanceLocator>();
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
services.AddSingleton<IMinecraftDownloadExecutor, MinecraftDownloadExecutor>();
services.AddSingleton<IMinecraftInstanceInstallationService, MinecraftInstanceInstallationService>();
services.AddSingleton<IMinecraftVersionCatalogService, MinecraftVersionCatalogService>();
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
            Console.WriteLine($"{instance.Name} | {instance.VersionId ?? "未知版本"} | {instance.Type ?? "未知类型"} | {instance.Status}");
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
        MinecraftAccount? account = null;
        if (args.Length == 3 && !OfflineAccount.TryCreate(args[2], out account))
        {
            Console.WriteLine("离线用户名需为 3–16 位英文字母、数字或下划线。");
            return 64;
        }

        var instancesTask = instanceCatalogService.GetAllAsync();
        var diagnosticsTask = diagnosticsService.GetAsync();
        await Task.WhenAll(instancesTask, diagnosticsTask);
        var instance = (await instancesTask).FirstOrDefault(candidate => candidate.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid);
        var java = (await diagnosticsTask).JavaInstallations.FirstOrDefault(candidate => candidate.IsCompatible);
        var preparation = await gameLaunchService.PrepareAsync(instance, account, java);
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
        Console.WriteLine("用法：info | java list | instances list | versions list | versions inspect | install local | launch check | launch arguments | launch run [离线用户名] | doctor");
        return command == "help" ? 0 : 64;
}

return 0;
