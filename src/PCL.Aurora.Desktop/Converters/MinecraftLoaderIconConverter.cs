using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Converters;

public sealed class MinecraftLoaderIconConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<MinecraftLoaderKind, Bitmap> Icons =
        new Dictionary<MinecraftLoaderKind, Bitmap>
        {
            [MinecraftLoaderKind.Forge] = Load("PclCeForge.png"),
            [MinecraftLoaderKind.NeoForge] = Load("PclCeNeoForge.png"),
            [MinecraftLoaderKind.Fabric] = Load("PclCeFabric.png"),
            [MinecraftLoaderKind.OptiFine] = Load("PclCeOptiFine.png"),
            [MinecraftLoaderKind.Cleanroom] = Load("PclCeCleanroom.png"),
            [MinecraftLoaderKind.LegacyFabric] = Load("PclCeFabric.png"),
            [MinecraftLoaderKind.LabyMod] = Load("PclCeLabyMod.png"),
            [MinecraftLoaderKind.LiteLoader] = Load("PclCeLiteLoader.png"),
        };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MinecraftLoaderKind kind && Icons.TryGetValue(kind, out var icon) ? icon : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static Bitmap Load(string fileName)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://PCL.Aurora.Desktop/Assets/Loaders/{fileName}"));
        return new Bitmap(stream);
    }
}
