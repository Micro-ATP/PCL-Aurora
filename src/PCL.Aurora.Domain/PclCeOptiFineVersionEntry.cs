// Directly adapted from PCL-CE, Plain Craft Launcher 2/Modules/Minecraft/ModDownload.cs.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: retains only validated public catalog fields.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public sealed record PclCeOptiFineVersionEntry(
    string FileName,
    string Type,
    string Patch,
    bool IsPreview,
    string? RequiredForgeVersion)
{
    public string DownloadPath => $"{Type}/{Patch.Replace(' ', '/')}";
}
