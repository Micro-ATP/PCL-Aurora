using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Converters;

public sealed class MinecraftVersionIconConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<MinecraftVersionCatalogCategory, Bitmap> Icons =
        new Dictionary<MinecraftVersionCatalogCategory, Bitmap>
        {
            [MinecraftVersionCatalogCategory.Release] = Load("Pcl2Grass.png"),
            [MinecraftVersionCatalogCategory.Snapshot] = Load("Pcl2CommandBlock.png"),
            [MinecraftVersionCatalogCategory.Legacy] = Load("PclCeCobbleStone.png"),
            [MinecraftVersionCatalogCategory.AprilFools] = Load("PclCeGoldBlock.png"),
        };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MinecraftVersionCatalogEntry version
            ? Icons[MinecraftVersionCatalogFilter.GetCategory(version)]
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static Bitmap Load(string fileName)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://PCL.Aurora.Desktop/Assets/Loaders/{fileName}"));
        return new Bitmap(stream);
    }
}
