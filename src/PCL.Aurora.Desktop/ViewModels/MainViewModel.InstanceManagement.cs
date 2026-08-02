using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel
{
    private MinecraftInstanceManagementSnapshot? managedInstanceSnapshot;
    private MinecraftInstanceIsolationMode managedEffectiveIsolationMode = MinecraftInstanceIsolationMode.All;

    public ObservableCollection<MinecraftInstanceContentEntry> ManagedInstanceContent { get; } = [];

    public ObservableCollection<MinecraftServerEntry> ManagedServers { get; } = [];

    public ObservableCollection<MinecraftModUpdateCandidate> ManagedModUpdates { get; } = [];

    public IReadOnlyList<MinecraftInstanceIsolationOverrideOption> ManagedInstanceIsolationModes { get; } =
    [
        new(null, "跟随全局设置"),
        new(MinecraftInstanceIsolationMode.Disabled, "关闭隔离"),
        new(MinecraftInstanceIsolationMode.ModdedOnly, "隔离可安装模组的实例"),
        new(MinecraftInstanceIsolationMode.NonReleaseOnly, "隔离非正式版"),
        new(MinecraftInstanceIsolationMode.ModdedAndNonRelease, "隔离模组与非正式版"),
        new(MinecraftInstanceIsolationMode.All, "隔离此实例"),
    ];

    [ObservableProperty]
    private MinecraftInstanceIsolationOverrideOption selectedManagedInstanceIsolationMode = new(null, "跟随全局设置");

    [ObservableProperty]
    private MinecraftInstanceContentEntry? selectedManagedInstanceContent;

    [ObservableProperty]
    private MinecraftServerEntry? selectedManagedServer;

    [ObservableProperty]
    private bool isInstanceManagementBusy;

    [ObservableProperty]
    private string instanceManagementStatus = "请选择一个实例。";

    [ObservableProperty]
    private string managedInstanceName = "未选择实例";

    [ObservableProperty]
    private string managedInstanceVersion = "—";

    [ObservableProperty]
    private string managedInstanceLoader = "—";

    [ObservableProperty]
    private string managedInstanceDirectory = "—";

    [ObservableProperty]
    private string managedGameDirectory = "—";

    [ObservableProperty]
    private string managedInstanceDescription = string.Empty;

    [ObservableProperty]
    private bool isManagedInstanceFavorite;

    [ObservableProperty]
    private string managedInstanceContentTitle = "资源";

    [ObservableProperty]
    private string managedInstanceContentSummary = "尚未读取。";

    [ObservableProperty]
    private int managedModCount;

    [ObservableProperty]
    private int managedResourcePackCount;

    [ObservableProperty]
    private int managedShaderPackCount;

    [ObservableProperty]
    private int managedSaveCount;

    [ObservableProperty]
    private int managedScreenshotCount;

    [ObservableProperty]
    private int managedSchematicCount;

    [ObservableProperty]
    private int managedServerCount;

    [ObservableProperty]
    private bool isManagedModSection;

    [ObservableProperty]
    private string managedModUpdateSummary = "尚未检查更新。";

    public bool HasManagedInstance => managedInstanceSnapshot is not null;

    public bool HasManagedInstanceContent => ManagedInstanceContent.Count > 0;

    public bool HasSelectedManagedServer => SelectedManagedServer is not null;

    public bool CanShowManagedModUpdate => IsManagedModSection && !HideFunctionModUpdate;

    public bool ShowManagedInstanceContentEmptyState =>
        !HasManagedInstanceContent && !IsInstanceManagementBusy;

    partial void OnSelectedManagedServerChanged(MinecraftServerEntry? value) =>
        OnPropertyChanged(nameof(HasSelectedManagedServer));

    partial void OnIsManagedModSectionChanged(bool value) =>
        OnPropertyChanged(nameof(CanShowManagedModUpdate));

    partial void OnIsInstanceManagementBusyChanged(bool value) =>
        OnPropertyChanged(nameof(ShowManagedInstanceContentEmptyState));

    public async Task LoadInstanceManagementAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedInstance is null)
        {
            ClearManagedInstanceState("当前没有可管理的实例。");
            return;
        }

        IsInstanceManagementBusy = true;
        InstanceManagementStatus = $"正在读取 {SelectedInstance.Name}…";
        try
        {
            managedInstanceSnapshot = await instanceManagement.InspectAsync(
                SelectedInstance,
                SelectedInstanceIsolationMode.Mode,
                cancellationToken);
            var snapshot = managedInstanceSnapshot;
            managedEffectiveIsolationMode = snapshot.EffectiveIsolationMode;
            ManagedInstanceName = snapshot.Instance.Name;
            ManagedInstanceVersion = snapshot.Instance.VersionDisplay;
            ManagedInstanceLoader = snapshot.Instance.LoaderDisplay;
            ManagedInstanceDirectory = snapshot.Instance.DirectoryPath;
            ManagedGameDirectory = snapshot.GameDirectory;
            ManagedInstanceDescription = snapshot.Profile.Description;
            IsManagedInstanceFavorite = snapshot.Profile.IsFavorite;
            SelectedManagedInstanceIsolationMode = ManagedInstanceIsolationModes.Single(option =>
                option.Mode == snapshot.Profile.IsolationMode);
            ManagedModCount = snapshot.GetCount(MinecraftInstanceContentKind.Mod);
            ManagedResourcePackCount = snapshot.GetCount(MinecraftInstanceContentKind.ResourcePack);
            ManagedShaderPackCount = snapshot.GetCount(MinecraftInstanceContentKind.ShaderPack);
            ManagedSaveCount = snapshot.GetCount(MinecraftInstanceContentKind.Save);
            ManagedScreenshotCount = snapshot.GetCount(MinecraftInstanceContentKind.Screenshot);
            ManagedSchematicCount = snapshot.GetCount(MinecraftInstanceContentKind.Schematic);
            ManagedServerCount = snapshot.ServerCount;
            InstanceManagementStatus = $"正在管理 {snapshot.Instance.Name}。";
            OnPropertyChanged(nameof(HasManagedInstance));
        }
        catch (Exception exception)
        {
            ClearManagedInstanceState($"读取实例失败：{exception.Message}");
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task LoadManagedContentAsync(
        MinecraftInstanceContentKind kind,
        CancellationToken cancellationToken = default)
    {
        if (SelectedInstance is null)
        {
            return;
        }

        IsInstanceManagementBusy = true;
        ManagedInstanceContentTitle = GetContentTitle(kind);
        IsManagedModSection = kind == MinecraftInstanceContentKind.Mod;
        ManagedInstanceContentSummary = $"正在读取{ManagedInstanceContentTitle}…";
        try
        {
            var items = await instanceManagement.GetContentAsync(
                SelectedInstance,
                managedEffectiveIsolationMode,
                kind,
                cancellationToken);
            ManagedInstanceContent.Clear();
            foreach (var item in items)
            {
                ManagedInstanceContent.Add(item);
            }
            SelectedManagedInstanceContent = null;
            OnPropertyChanged(nameof(HasManagedInstanceContent));
            OnPropertyChanged(nameof(ShowManagedInstanceContentEmptyState));
            ManagedInstanceContentSummary = items.Count == 0
                ? $"当前实例还没有{ManagedInstanceContentTitle}。"
                : $"共 {items.Count} 项{ManagedInstanceContentTitle}。";
        }
        catch (Exception exception)
        {
            ManagedInstanceContent.Clear();
            OnPropertyChanged(nameof(HasManagedInstanceContent));
            OnPropertyChanged(nameof(ShowManagedInstanceContentEmptyState));
            ManagedInstanceContentSummary = $"读取失败：{exception.Message}";
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task ImportManagedContentAsync(
        MinecraftInstanceContentKind kind,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        IsInstanceManagementBusy = true;
        try
        {
            var result = await instanceManagement.ImportAsync(
                instance,
                managedEffectiveIsolationMode,
                kind,
                sourcePaths,
                cancellationToken);
            InstanceManagementStatus = $"已导入 {result.ImportedCount} 项{GetContentTitle(kind)}。";
            await LoadInstanceManagementAsync(cancellationToken);
            await LoadManagedContentAsync(kind, cancellationToken);
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task ToggleManagedContentAsync(
        MinecraftInstanceContentEntry entry,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.SetContentEnabledAsync(
            instance,
            managedEffectiveIsolationMode,
            entry.Kind,
            entry.RelativePath,
            !entry.IsEnabled,
            cancellationToken);
        await LoadManagedContentAsync(entry.Kind, cancellationToken);
        await LoadInstanceManagementAsync(cancellationToken);
    }

    public async Task<MinecraftModUpdateCheckResult> CheckManagedModUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        IsInstanceManagementBusy = true;
        ManagedModUpdateSummary = "正在识别本地 Mod 并检查兼容更新…";
        try
        {
            var result = await modUpdates.CheckAsync(
                instance,
                managedEffectiveIsolationMode,
                cancellationToken);
            ManagedModUpdates.Clear();
            foreach (var update in result.Updates)
            {
                ManagedModUpdates.Add(update);
            }
            ManagedModUpdateSummary = result.Updates.Count == 0
                ? $"已识别 {result.RecognizedCount} 个 Mod，没有发现兼容更新。"
                : $"发现 {result.Updates.Count} 项兼容更新；已识别 {result.RecognizedCount} 个，未识别 {result.UnrecognizedCount} 个。";
            return result;
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task<MinecraftModUpdateApplyResult> ApplyManagedModUpdatesAsync(
        IReadOnlyList<MinecraftModUpdateCandidate> updates,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        IsInstanceManagementBusy = true;
        try
        {
            var progress = new Progress<MinecraftDownloadProgress>(update =>
                ManagedModUpdateSummary = $"正在更新：{update.CurrentDescription}（{update.CompletedArtifacts}/{update.TotalArtifacts}）");
            var result = await modUpdates.ApplyAsync(
                instance,
                managedEffectiveIsolationMode,
                updates,
                progress,
                cancellationToken);
            ManagedModUpdateSummary = $"已更新 {result.UpdatedCount} 个 Mod。";
            ManagedModUpdates.Clear();
            await LoadManagedContentAsync(MinecraftInstanceContentKind.Mod, cancellationToken);
            await LoadInstanceManagementAsync(cancellationToken);
            return result;
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task DeleteManagedContentAsync(
        MinecraftInstanceContentEntry entry,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.DeleteContentAsync(
            instance,
            managedEffectiveIsolationMode,
            entry.Kind,
            entry.RelativePath,
            cancellationToken);
        await LoadManagedContentAsync(entry.Kind, cancellationToken);
        await LoadInstanceManagementAsync(cancellationToken);
    }

    public Task ExportManagedContentAsync(
        MinecraftInstanceContentEntry entry,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        return instanceManagement.ExportContentAsync(
            instance,
            managedEffectiveIsolationMode,
            entry.Kind,
            entry.RelativePath,
            destinationPath,
            cancellationToken);
    }

    public async Task OpenManagedContentDirectoryAsync(
        MinecraftInstanceContentKind kind,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        var path = instanceManagement.GetContentDirectory(instance, managedEffectiveIsolationMode, kind);
        Directory.CreateDirectory(path);
        await openPathService.OpenFolderAsync(path, cancellationToken);
    }

    public async Task OpenManagedInstanceDirectoryAsync(bool gameDirectory, CancellationToken cancellationToken = default)
    {
        var snapshot = managedInstanceSnapshot ?? throw new InvalidOperationException("实例信息尚未读取。");
        var path = gameDirectory ? snapshot.GameDirectory : snapshot.Instance.DirectoryPath;
        Directory.CreateDirectory(path);
        await openPathService.OpenFolderAsync(path, cancellationToken);
    }

    public async Task SaveManagedProfileAsync(CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        var profile = new MinecraftInstanceProfile(
            ManagedInstanceDescription.Trim(),
            IsManagedInstanceFavorite,
            SelectedManagedInstanceIsolationMode.Mode);
        await instanceManagement.SaveProfileAsync(instance, profile, cancellationToken);
        InstanceManagementStatus = "实例显示信息已保存。";
        await LoadInstanceManagementAsync(cancellationToken);
    }

    public async Task RenameManagedInstanceAsync(string newName, CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.RenameAsync(instance, newName, cancellationToken);
        await RefreshAsync();
        SelectedInstance = AvailableInstances.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, newName, StringComparison.Ordinal));
        await LoadInstanceManagementAsync(cancellationToken);
        InstanceManagementStatus = $"实例已重命名为 {newName}。";
    }

    public async Task CopyManagedInstanceAsync(string newName, CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.CopyAsync(instance, newName, cancellationToken);
        await RefreshAsync();
        SelectedInstance = AvailableInstances.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, newName, StringComparison.Ordinal));
        await LoadInstanceManagementAsync(cancellationToken);
        InstanceManagementStatus = $"已复制并切换到实例 {newName}。";
    }

    public Task<MinecraftInstanceArchiveResult> ExportManagedInstanceAsync(
        string destinationPath,
        bool includeGameData,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        return instanceManagement.ExportInstanceAsync(
            instance,
            managedEffectiveIsolationMode,
            destinationPath,
            includeGameData,
            cancellationToken);
    }

    public async Task DeleteManagedInstanceAsync(CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.DeleteInstanceAsync(instance, cancellationToken);
        await RefreshAsync();
        ClearManagedInstanceState($"实例 {instance.Name} 已删除。共享游戏目录中的数据未被删除。");
    }

    public async Task RepairManagedInstanceAsync()
    {
        var installed = await InstallGameCoreAsync(refreshDefaultInstanceCatalog: false, SelectedInstance);
        InstanceManagementStatus = installed ? "实例文件检查与修复完成。" : InstallationSummary;
        await LoadInstanceManagementAsync();
    }

    public Task TestManagedInstanceAsync() => LaunchGameAsync();

    public async Task LoadManagedServersAsync(CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        IsInstanceManagementBusy = true;
        try
        {
            var servers = await instanceManagement.GetServersAsync(
                instance,
                managedEffectiveIsolationMode,
                cancellationToken);
            ManagedServers.Clear();
            foreach (var server in servers)
            {
                ManagedServers.Add(server);
            }
            SelectedManagedServer = null;
            ManagedServerCount = servers.Count;
            InstanceManagementStatus = servers.Count == 0 ? "当前实例没有保存服务器。" : $"共 {servers.Count} 个服务器。";
        }
        finally
        {
            IsInstanceManagementBusy = false;
        }
    }

    public async Task SaveManagedServersAsync(
        IReadOnlyList<MinecraftServerEntry> servers,
        CancellationToken cancellationToken = default)
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("当前没有选中的实例。");
        await instanceManagement.SaveServersAsync(
            instance,
            managedEffectiveIsolationMode,
            servers,
            cancellationToken);
        await LoadManagedServersAsync(cancellationToken);
        await LoadInstanceManagementAsync(cancellationToken);
    }

    private void ClearManagedInstanceState(string status)
    {
        managedInstanceSnapshot = null;
        ManagedInstanceName = "未选择实例";
        ManagedInstanceVersion = "—";
        ManagedInstanceLoader = "—";
        ManagedInstanceDirectory = "—";
        ManagedGameDirectory = "—";
        ManagedInstanceDescription = string.Empty;
        IsManagedInstanceFavorite = false;
        SelectedManagedInstanceIsolationMode = ManagedInstanceIsolationModes[0];
        ManagedInstanceContent.Clear();
        OnPropertyChanged(nameof(HasManagedInstanceContent));
        OnPropertyChanged(nameof(ShowManagedInstanceContentEmptyState));
        ManagedServers.Clear();
        ManagedModUpdates.Clear();
        IsManagedModSection = false;
        ManagedModUpdateSummary = "尚未检查更新。";
        ManagedModCount = 0;
        ManagedResourcePackCount = 0;
        ManagedShaderPackCount = 0;
        ManagedSaveCount = 0;
        ManagedScreenshotCount = 0;
        ManagedSchematicCount = 0;
        ManagedServerCount = 0;
        InstanceManagementStatus = status;
        OnPropertyChanged(nameof(HasManagedInstance));
    }

    private static string GetContentTitle(MinecraftInstanceContentKind kind) => kind switch
    {
        MinecraftInstanceContentKind.Mod => "Mod",
        MinecraftInstanceContentKind.ResourcePack => "资源包",
        MinecraftInstanceContentKind.ShaderPack => "光影包",
        MinecraftInstanceContentKind.Save => "存档",
        MinecraftInstanceContentKind.Screenshot => "截图",
        MinecraftInstanceContentKind.Schematic => "投影原理图",
        _ => "资源",
    };
}

public sealed record MinecraftInstanceIsolationOverrideOption(
    MinecraftInstanceIsolationMode? Mode,
    string DisplayName);
