namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionDownload(Uri Url, string? Sha1, long? Size);
