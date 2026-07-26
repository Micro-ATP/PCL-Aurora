using System.Threading.Channels;

namespace PCL.Aurora.Application;

public sealed record GameProcessSession(
    int ProcessId,
    ChannelReader<GameProcessOutput> Output,
    Task<int> ExitCode);
