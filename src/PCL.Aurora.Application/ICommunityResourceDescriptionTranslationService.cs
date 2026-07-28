using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceDescriptionTranslationService
{
    Task<CommunityResourceDescriptionTranslationResult> TranslateAsync(
        CommunityResourceProject project,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityResourceDescriptionTranslationResult(string? Translation, string? Error)
{
    public bool HasTranslation => !string.IsNullOrWhiteSpace(Translation);

    public static CommunityResourceDescriptionTranslationResult Success(string translation) => new(translation, null);

    public static CommunityResourceDescriptionTranslationResult Failure(string error) => new(null, error);
}
