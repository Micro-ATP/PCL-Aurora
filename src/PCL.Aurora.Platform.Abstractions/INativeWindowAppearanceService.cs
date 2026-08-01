namespace PCL.Aurora.Platform.Abstractions;

public interface INativeWindowAppearanceService
{
    bool TryEnableTransparentBackground(nint nativeWindowHandle);
}
