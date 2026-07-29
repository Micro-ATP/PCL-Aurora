namespace PCL.Aurora.Application;

public interface IGitHubContributorService
{
    Task<IReadOnlyList<GitHubContributor>> GetContributorsAsync(
        CancellationToken cancellationToken = default);
}
