using Android.Animation;

namespace OverwatchRandomizer.Modern.Core;

public static partial class Motion
{
    public static partial bool Enabled => !OperatingSystem.IsAndroidVersionAtLeast(26) || ValueAnimator.AreAnimatorsEnabled();
}
