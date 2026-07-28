using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceLocalizationService
{
    CommunityResourceProject Localize(CommunityResourceProject project);
}
