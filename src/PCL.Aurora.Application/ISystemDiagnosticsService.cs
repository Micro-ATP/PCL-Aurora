namespace PCL.Aurora.Application;

public interface ISystemDiagnosticsService
{
    Task<SystemDiagnostics> GetAsync(CancellationToken cancellationToken = default);
}
