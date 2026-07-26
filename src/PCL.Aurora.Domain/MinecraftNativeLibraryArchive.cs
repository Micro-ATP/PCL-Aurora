namespace PCL.Aurora.Domain;

public sealed record MinecraftNativeLibraryArchive(
    string LibraryName,
    string Classifier,
    string LocalPath,
    MinecraftVersionDownload Download);
