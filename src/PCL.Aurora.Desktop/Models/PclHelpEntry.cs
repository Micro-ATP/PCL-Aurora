namespace PCL.Aurora.Desktop.Models;

internal sealed record PclHelpEntry(
    string Path,
    string Title,
    string Description,
    string Keywords,
    IReadOnlyList<string> Categories,
    string? Logo,
    bool ShowInSearch,
    bool ShowInPublic,
    bool ShowInSnapshot,
    bool IsEvent,
    string? EventType,
    string? EventData,
    string? Content);

internal sealed record PclHelpAction(string Type, string Data);
