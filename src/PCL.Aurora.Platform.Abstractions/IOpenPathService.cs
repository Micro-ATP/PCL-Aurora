namespace PCL.Aurora.Platform.Abstractions;

public interface IOpenPathService
{
    Task OpenFolderAsync(string path, CancellationToken cancellationToken = default);

    Task OpenUriAsync(Uri uri, CancellationToken cancellationToken = default);
}
