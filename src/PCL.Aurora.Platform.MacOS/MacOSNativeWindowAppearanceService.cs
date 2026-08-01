using System.Runtime.InteropServices;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSNativeWindowAppearanceService : INativeWindowAppearanceService
{
    private const string ObjectiveCRuntime = "/usr/lib/libobjc.A.dylib";
    private const double TransparentAlphaThreshold = 0.001;

    public bool TryEnableTransparentBackground(nint nativeWindowHandle)
    {
        if (!OperatingSystem.IsMacOS() || nativeWindowHandle == 0)
        {
            return false;
        }

        var clearColor = SendPointer(
            ObjectiveCClass("NSColor"),
            ObjectiveCSelector("clearColor"));
        if (clearColor == 0)
        {
            return false;
        }

        SendByte(nativeWindowHandle, ObjectiveCSelector("setOpaque:"), 0);
        SendPointerArgument(nativeWindowHandle, ObjectiveCSelector("setBackgroundColor:"), clearColor);

        var contentView = SendPointer(nativeWindowHandle, ObjectiveCSelector("contentView"));
        if (contentView != 0)
        {
            SendByte(contentView, ObjectiveCSelector("setWantsLayer:"), 1);
            var layer = SendPointer(contentView, ObjectiveCSelector("layer"));
            if (layer != 0)
            {
                SendByte(layer, ObjectiveCSelector("setOpaque:"), 0);
                SendPointerArgument(layer, ObjectiveCSelector("setBackgroundColor:"), 0);
            }
        }

        var appliedBackgroundColor = SendPointer(
            nativeWindowHandle,
            ObjectiveCSelector("backgroundColor"));
        return SendByteResult(nativeWindowHandle, ObjectiveCSelector("isOpaque")) == 0 &&
               appliedBackgroundColor != 0 &&
               SendDouble(appliedBackgroundColor, ObjectiveCSelector("alphaComponent")) <= TransparentAlphaThreshold;
    }

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_getClass", CharSet = CharSet.Ansi)]
    private static extern nint ObjectiveCClass(string name);

    [DllImport(ObjectiveCRuntime, EntryPoint = "sel_registerName", CharSet = CharSet.Ansi)]
    private static extern nint ObjectiveCSelector(string name);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static extern nint SendPointer(nint receiver, nint selector);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static extern void SendPointerArgument(nint receiver, nint selector, nint value);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static extern void SendByte(nint receiver, nint selector, byte value);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static extern byte SendByteResult(nint receiver, nint selector);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend")]
    private static extern double SendDouble(nint receiver, nint selector);
}
