namespace PCL.Aurora.Desktop.ViewModels;

public sealed record LicenseEntryViewModel(
    string Name,
    string Information,
    string? WebsiteTarget,
    string? LicenseTarget)
{
    public bool HasWebsite => !string.IsNullOrWhiteSpace(WebsiteTarget);

    public bool HasLicense => !string.IsNullOrWhiteSpace(LicenseTarget);
}
