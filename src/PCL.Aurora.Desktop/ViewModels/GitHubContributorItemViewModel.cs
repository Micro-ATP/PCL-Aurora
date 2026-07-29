using Avalonia.Media.Imaging;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed class GitHubContributorItemViewModel : ViewModelBase, IDisposable
{
    public GitHubContributorItemViewModel(PCL.Aurora.Application.GitHubContributor contributor)
    {
        Login = contributor.Login;
        ProfileUri = contributor.ProfileUri;
        Contributions = contributor.Contributions;

        if (contributor.AvatarBytes is { Length: > 0 } bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            Avatar = new Bitmap(stream);
        }
    }

    public string Login { get; }

    public Uri ProfileUri { get; }

    public int Contributions { get; }

    public string Initial => Login.Length > 0 ? Login[..1].ToUpperInvariant() : "?";

    public Bitmap? Avatar { get; }

    public bool HasAvatar => Avatar is not null;

    public void Dispose() => Avatar?.Dispose();
}
