// Directly adapts PCL-CE Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs.
// The bundled mcmod.buf resource is copied unchanged from PCL-CE and decoded
// with the same protobuf-net format. Modified by Micro-ATP for in-memory lookup.
using System.IO.Compression;
using System.Reflection;
using PCL.Aurora.Domain;
using ProtoBuf;

namespace PCL.Aurora.Application;

public sealed class PclCeCommunityResourceLocalizationService : ICommunityResourceLocalizationService
{
    private const string ResourceName = "PCL.Aurora.Application.Assets.PclCeMcModTranslations.buf";
    private readonly Lazy<IReadOnlyDictionary<string, string>> translations = new(TryLoadTranslations);

    public CommunityResourceProject Localize(CommunityResourceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Type is not (CommunityResourceType.Mod or CommunityResourceType.DataPack) ||
            !translations.Value.TryGetValue(project.Slug, out var translatedTitle) ||
            string.IsNullOrWhiteSpace(translatedTitle))
        {
            return project;
        }

        return project with { TranslatedTitle = translatedTitle.Trim() };
    }

    private static IReadOnlyDictionary<string, string> TryLoadTranslations()
    {
        try
        {
            return LoadTranslations();
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ProtoException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, string> LoadTranslations()
    {
        using var compressed = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("PCL-CE 中文模组译名资源未随应用发布。");
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        var entries = Serializer.Deserialize<List<TranslationEntry>>(decompressed);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            Add(entry.ModrinthSlug, entry.ChineseName);
            Add(entry.CurseForgeSlug, entry.ChineseName);
        }

        return result;

        void Add(string? slug, string? chineseName)
        {
            if (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(chineseName))
            {
                result.TryAdd(slug.Trim(), chineseName.Trim());
            }
        }
    }

    [ProtoContract]
    private sealed class TranslationEntry
    {
        [ProtoMember(1)]
        public int WikiId { get; set; }

        [ProtoMember(2)]
        public string? ChineseName { get; set; }

        [ProtoMember(3)]
        public string? CurseForgeSlug { get; set; }

        [ProtoMember(4)]
        public string? ModrinthSlug { get; set; }
    }
}
