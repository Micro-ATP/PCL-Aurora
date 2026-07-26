using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

var services = new ServiceCollection();
services.AddSingleton<IPlatformInfo, MacOSPlatformInfo>();
services.AddSingleton<IPlatformPaths, MacOSPlatformPaths>();
services.AddSingleton<IJavaLocator, MacOSJavaLocator>();
services.AddSingleton<IMinecraftInstanceLocator, MacOSMinecraftInstanceLocator>();
services.AddSingleton<IOpenPathService, MacOSOpenPathService>();
services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
services.AddSingleton<IInstanceCatalogService, InstanceCatalogService>();
services.AddSingleton<ILaunchReadinessService, LaunchReadinessService>();

await using var provider = services.BuildServiceProvider();
var diagnosticsService = provider.GetRequiredService<ISystemDiagnosticsService>();
var instanceCatalogService = provider.GetRequiredService<IInstanceCatalogService>();
var launchReadinessService = provider.GetRequiredService<ILaunchReadinessService>();

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
    default:
        Console.WriteLine("PCL Aurora 诊断工具");
        Console.WriteLine("用法：info | java list | instances list | launch check | doctor");
        return command == "help" ? 0 : 64;
}

return 0;
