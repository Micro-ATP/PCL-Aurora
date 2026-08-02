using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftInstanceManagementService
{
    Task<MinecraftInstanceProfile> GetProfileAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default);

    Task<MinecraftInstanceManagementSnapshot> InspectAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MinecraftInstanceContentEntry>> GetContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        CancellationToken cancellationToken = default);

    Task<MinecraftInstanceImportResult> ImportAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default);

    Task SetContentEnabledAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        bool enabled,
        CancellationToken cancellationToken = default);

    Task DeleteContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task ExportContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MinecraftServerEntry>> GetServersAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default);

    Task SaveServersAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        IReadOnlyList<MinecraftServerEntry> servers,
        CancellationToken cancellationToken = default);

    Task SaveProfileAsync(
        MinecraftInstance instance,
        MinecraftInstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<string> RenameAsync(
        MinecraftInstance instance,
        string newName,
        CancellationToken cancellationToken = default);

    Task<string> CopyAsync(
        MinecraftInstance instance,
        string newName,
        CancellationToken cancellationToken = default);

    Task<MinecraftInstanceArchiveResult> ExportInstanceAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        string destinationPath,
        bool includeGameData,
        CancellationToken cancellationToken = default);

    Task DeleteInstanceAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default);

    string GetContentDirectory(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind);
}
