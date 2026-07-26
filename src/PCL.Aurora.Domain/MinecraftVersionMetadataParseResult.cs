namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionMetadataParseResult(
    MinecraftVersionMetadata? Metadata,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Metadata is not null && Errors.Count == 0;
}
