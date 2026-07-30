namespace PCL.Aurora.Desktop.Services;

internal static class PclMotionSettings
{
    private static int framesPerSecond = 60;
    private static double speedMultiplier = 1;

    public static bool IsEnabled => Volatile.Read(ref speedMultiplier) > 0;

    public static double SpeedMultiplier => Volatile.Read(ref speedMultiplier);

    public static TimeSpan FrameInterval =>
        TimeSpan.FromMilliseconds(1000d / Math.Clamp(Volatile.Read(ref framesPerSecond), 1, 60));

    public static void Configure(int frameLimit, double speed)
    {
        Volatile.Write(ref framesPerSecond, Math.Clamp(frameLimit, 1, 60));
        Volatile.Write(ref speedMultiplier, Math.Clamp(speed, 0, 3));
    }

    public static TimeSpan Scale(TimeSpan duration)
    {
        var speed = SpeedMultiplier;
        return speed <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)(duration.Ticks / speed));
    }
}
