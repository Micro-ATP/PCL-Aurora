namespace PCL.Aurora.Domain;

public sealed record CommunityResourceVersionFile(
    string FileName,
    Uri Url,
    string Sha1,
    long Size,
    bool IsPrimary);
