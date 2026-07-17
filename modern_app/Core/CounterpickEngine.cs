using System.Text;
using System.Text.Json;

namespace OverwatchRandomizer.Modern.Core;

public enum CounterpickMode { FiveVsFive, Open, Stadium }

public sealed record CounterpickPick(Hero Hero, int Score);
public sealed record CounterpickTeam(IReadOnlyList<CounterpickPick> Picks, int Score);

public static class CounterpickEngine
{
    public static IReadOnlyList<string> MatchHeroes(string input) => input
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(name => HeroCatalog.All.FirstOrDefault(hero => Normalize(hero.Name) == Normalize(name))?.Name)
        .Where(name => name is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<CounterpickTeam> Calculate(
        string json, IEnumerable<string> enemies, CounterpickMode mode, int count = 5)
    {
        using var document = JsonDocument.Parse(json);
        var heroes = document.RootElement.GetProperty("heroes");
        var source = heroes.EnumerateObject().ToDictionary(item => Normalize(item.Name), item => item.Value);
        var scores = HeroCatalog.All.ToDictionary(hero => hero.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var enemy in enemies.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!source.TryGetValue(Normalize(enemy), out var data)) continue;
            foreach (var counter in data.GetProperty("countered_by").EnumerateArray())
            {
                var name = counter.GetProperty("hero").GetString();
                if (name is not null && scores.ContainsKey(name)) scores[name] += counter.GetProperty("strength").GetInt32();
            }
        }

        var pool = HeroCatalog.All.Where(hero => mode != CounterpickMode.Stadium || hero.Stadium).ToArray();
        var roleCounts = mode == CounterpickMode.Open
            ? new[] { (Role.Tank, 2), (Role.Damage, 2), (Role.Support, 2) }
            : new[] { (Role.Tank, 1), (Role.Damage, 2), (Role.Support, 2) };
        var used = new HashSet<Hero>();
        var result = new List<CounterpickTeam>();
        for (var teamIndex = 0; teamIndex < count; teamIndex++)
        {
            var picks = roleCounts.SelectMany(rule => pool.Where(hero => hero.Role == rule.Item1 && !used.Contains(hero))
                    .OrderByDescending(hero => scores[hero.Name]).ThenBy(hero => hero.Name, StringComparer.Ordinal).Take(rule.Item2))
                .Select(hero => new CounterpickPick(hero, scores[hero.Name])).ToArray();
            if (picks.Length != roleCounts.Sum(rule => rule.Item2)) break;
            result.Add(new CounterpickTeam(picks, picks.Sum(pick => pick.Score)));
            foreach (var pick in picks) used.Add(pick.Hero);
        }
        return result;
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value.ToLowerInvariant()) if (char.IsLetterOrDigit(character)) result.Append(character);
        return result.ToString();
    }
}
