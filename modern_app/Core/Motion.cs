namespace OverwatchRandomizer.Modern.Core;

public static partial class Motion
{
    public static partial bool Enabled { get; }
    public static uint Duration => Enabled ? 180u : 0u;
}
