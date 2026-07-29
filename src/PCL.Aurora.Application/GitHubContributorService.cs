using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Aurora.Application;

public sealed class GitHubContributorService(HttpClient httpClient) : IGitHubContributorService
{
    private static readonly Uri ContributorsUri =
        new("https://api.github.com/repos/Micro-ATP/PCL-Aurora/contributors?per_page=100");
    private static readonly ProductInfoHeaderValue UserAgent = new("PCL-Aurora", "1.0");

    public async Task<IReadOnlyList<GitHubContributor>> GetContributorsAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(ContributorsUri);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var entries = await JsonSerializer.DeserializeAsync<List<ContributorResponse>>(
            content,
            cancellationToken: cancellationToken) ?? [];

        var contributors = new GitHubContributor[entries.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, entries.Count),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 6,
            },
            async (index, token) =>
            {
                var entry = entries[index];
                contributors[index] = new GitHubContributor(
                    entry.Login,
                    entry.ProfileUri,
                    entry.Contributions,
                    await TryLoadAvatarAsync(entry.AvatarUri, token));
            });

        return contributors;
    }

    private async Task<byte[]?> TryLoadAvatarAsync(Uri avatarUri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(avatarUri);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > 1_048_576)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private sealed record ContributorResponse(
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("avatar_url")] Uri AvatarUri,
        [property: JsonPropertyName("html_url")] Uri ProfileUri,
        [property: JsonPropertyName("contributions")] int Contributions);
}
