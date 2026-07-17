using Windows.UI.ViewManagement;

namespace OverwatchRandomizer.Modern.Core;

public static partial class Motion
{
    public static partial bool Enabled
    {
        get
        {
            try { return new UISettings().AnimationsEnabled; }
            catch { return true; }
        }
    }
}
