using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

var services = new ServiceCollection();
services.AddSingleton<IPlatformInfo, MacOSPlatformInfo>();
services.AddSingleton<IPlatformPaths, MacOSPlatformPaths>();
services.AddSingleton<IJavaLocator, MacOSJavaLocator>();
services.AddSingleton<IOpenPathService, MacOSOpenPathService>();
services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();

await using var provider = services.BuildServiceProvider();
var diagnosticsService = provider.GetRequiredService<ISystemDiagnosticsService>();

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
    default:
        Console.WriteLine("PCL Aurora 诊断工具");
        Console.WriteLine("用法：info | java list | doctor");
        return command == "help" ? 0 : 64;
}

return 0;
