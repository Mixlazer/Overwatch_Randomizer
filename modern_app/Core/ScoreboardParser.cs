using System.Text;
using System.Text.RegularExpressions;

namespace OverwatchRandomizer.Modern.Core;

public static partial class ScoreboardParser
{
    [GeneratedRegex(@"(?<![\w:])(?:\d+:\d{1,2}|\d{1,3}(?:,\d{3})+|\d+)(?![\w:])")]
    private static partial Regex NumberPattern();

    public static Dictionary<int, RoundStats> Parse(IEnumerable<string> rows, IReadOnlyList<string> playerNames)
    {
        var candidates = rows.Select((row, index) => (row, index, numbers: NumberPattern().Matches(row).Select(match => match.Value).ToArray()))
            .Where(item => item.numbers.Length >= 5).ToArray();
        var found = new Dictionary<int, RoundStats>();
        var usedRows = new HashSet<int>();
        for (var playerIndex = 0; playerIndex < playerNames.Count; playerIndex++)
        {
            var best = candidates.Select(item => (score: NameScore(item.row, playerNames[playerIndex]), item))
                .Where(item => !usedRows.Contains(item.item.index)).OrderByDescending(item => item.score).FirstOrDefault();
            if (best.score < 0.62) continue;
            var numbers = best.item.numbers[^5..];
            found[playerIndex] = new RoundStats
            {
                Eliminations = ParseNumber(numbers[0]),
                ObjectiveSeconds = ParseObjective(numbers[1]),
                Damage = ParseNumber(numbers[2]),
                Healing = ParseNumber(numbers[3]),
                Deaths = ParseNumber(numbers[4]),
            };
            usedRows.Add(best.item.index);
        }
        return found;
    }

    public static int ParseObjective(string value)
    {
        var text = value.Trim().Replace(" ", "").Replace(",", "");
        if (!text.Contains(':')) return ParseNumber(text);
        var pieces = text.Split(':');
        if (pieces.Length != 2 || !int.TryParse(pieces[0], out var minutes) ||
            !int.TryParse(pieces[1], out var seconds) || seconds >= 60) throw new FormatException(value);
        return minutes * 60 + seconds;
    }

    public static int ParseNumber(string value)
    {
        var text = value.Trim().Replace(" ", "").Replace(",", "");
        if (!int.TryParse(text, out var number) || number < 0) throw new FormatException(value);
        return number;
    }

    private static double NameScore(string row, string name)
    {
        var cleanName = Normalize(name);
        var cleanRow = Normalize(row);
        if (cleanName.Length == 0) return 0;
        if (cleanRow.Contains(cleanName, StringComparison.Ordinal)) return 1;
        return Regex.Matches(row, @"[\w.-]+")
            .Select(match => Similarity(cleanName, Normalize(match.Value))).DefaultIfEmpty(0).Max();
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value.ToLowerInvariant()) if (char.IsLetterOrDigit(character)) result.Append(character);
        return result.ToString();
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return 0;
        var costs = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var saved = costs[j];
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), previous + (left[i - 1] == right[j - 1] ? 0 : 1));
                previous = saved;
            }
        }
        return 1d - (double)costs[^1] / Math.Max(left.Length, right.Length);
    }
}
