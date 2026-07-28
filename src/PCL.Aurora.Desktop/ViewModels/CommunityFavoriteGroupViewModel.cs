using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed record CommunityFavoriteGroupViewModel(
    CommunityResourceType Type,
    string Title,
    IReadOnlyList<CommunityResourceItemViewModel> Items)
{
    public int Count => Items.Count;
}
