namespace OverwatchRandomizer.Modern.Core;

public static class ScoreboardOcr
{
    public static Task<Dictionary<int, RoundStats>> ReadAsync(FileResult file, IReadOnlyList<string> playerNames) =>
        LocalVlm.ReadStatsAsync(file, playerNames);
}
