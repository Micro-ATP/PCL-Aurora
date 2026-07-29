namespace PCL.Aurora.Application;

public sealed record GitHubContributor(
    string Login,
    Uri ProfileUri,
    int Contributions,
    byte[]? AvatarBytes);
