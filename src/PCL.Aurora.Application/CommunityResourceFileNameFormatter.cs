using System.Text;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 复用 PCL-CE CompFileNameGet 的社区文件名规则，并保留跨平台安全文件名边界。
/// </summary>
public static class CommunityResourceFileNameFormatter
{
    public static string Format(
        CommunityResourceProject project,
        CommunityResourceVersionFile file,
        CommunityFileNameFormat format)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(file);
        var original = Sanitize(file.FileName);
        var translated = Sanitize(project.DisplayTitle);
        if (!project.HasTranslatedTitle || string.Equals(translated, Sanitize(project.Title), StringComparison.Ordinal))
        {
            return ReplaceModTilde(original, project.Type);
        }

        var result = format switch
        {
            CommunityFileNameFormat.ChineseBrackets => $"【{translated}】{original}",
            CommunityFileNameFormat.SquareBrackets => $"[{translated}] {original}",
            CommunityFileNameFormat.TranslatedNameFirst => $"{translated}-{original}",
            CommunityFileNameFormat.OriginalNameFirst => $"{original}-{translated}",
            _ => original,
        };
        return ReplaceModTilde(Sanitize(result), project.Type);
    }

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "download";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => '＼',
                '/' => '／',
                ':' => '：',
                '*' => '＊',
                '?' => '？',
                '"' => '＂',
                '<' => '＜',
                '>' => '＞',
                '|' => '｜',
                _ when char.IsControl(character) => '_',
                _ => character,
            });
        }

        var result = builder.ToString().Trim();
        return result is "" or "." or ".." ? "download" : result;
    }

    private static string ReplaceModTilde(string fileName, CommunityResourceType type) =>
        type == CommunityResourceType.Mod ? fileName.Replace('~', '-') : fileName;
}
