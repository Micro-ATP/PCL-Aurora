namespace PCL.Aurora.Domain;

public sealed record CommunityFavoriteFolder(
    string Id,
    string Name,
    IReadOnlyList<CommunityResourceProject> Projects)
{
    public const int MaximumNameLength = 60;
    public const int MaximumProjectCount = 5000;

    public bool IsValid =>
        Guid.TryParse(Id, out _) &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= MaximumNameLength &&
        !Name.Any(char.IsControl) &&
        Projects is not null &&
        Projects.Count <= MaximumProjectCount &&
        Projects.All(project => !string.IsNullOrWhiteSpace(project.Id)) &&
        Projects.Select(project => project.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Projects.Count;

    public bool Contains(string projectId) =>
        Projects.Any(project => string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase));

    public static CommunityFavoriteFolder Create(string name, IEnumerable<CommunityResourceProject>? projects = null) =>
        new(
            Guid.NewGuid().ToString("D"),
            name.Trim(),
            (projects ?? [])
                .GroupBy(project => project.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(MaximumProjectCount)
                .ToArray());
}
