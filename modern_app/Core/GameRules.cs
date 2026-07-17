using System.Globalization;
using System.Text;

namespace OverwatchRandomizer.Modern.Core;

public enum Role { Tank, Damage, Support }
public enum HeroPool { Standard, Stadium }
public enum RandomizerMode { FiveVsFive, Open, Stadium, Custom }

public sealed record Hero(string Name, Role Role, bool Stadium)
{
    public string ImageName => $"hero_{Slug(Name)}.png";

    private static string Slug(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }
}

public static class HeroCatalog
{
    public static readonly IReadOnlyList<Hero> All =
    [
        new("D.Va", Role.Tank, true), new("Domina", Role.Tank, false),
        new("Doomfist", Role.Tank, true), new("Hazard", Role.Tank, true),
        new("Junker Queen", Role.Tank, true), new("Mauga", Role.Tank, false),
        new("Orisa", Role.Tank, true), new("Ramattra", Role.Tank, true),
        new("Reinhardt", Role.Tank, true), new("Roadhog", Role.Tank, false),
        new("Sigma", Role.Tank, true), new("Winston", Role.Tank, true),
        new("Wrecking Ball", Role.Tank, false), new("Zarya", Role.Tank, true),

        new("Anran", Role.Damage, false), new("Ashe", Role.Damage, true),
        new("Bastion", Role.Damage, false), new("Cassidy", Role.Damage, true),
        new("Echo", Role.Damage, false), new("Emre", Role.Damage, false),
        new("Freja", Role.Damage, true), new("Genji", Role.Damage, true),
        new("Hanzo", Role.Damage, false), new("Junkrat", Role.Damage, true),
        new("Mei", Role.Damage, true), new("Pharah", Role.Damage, true),
        new("Reaper", Role.Damage, true), new("Shion", Role.Damage, false),
        new("Sierra", Role.Damage, false), new("Sojourn", Role.Damage, true),
        new("Soldier: 76", Role.Damage, true), new("Sombra", Role.Damage, false),
        new("Symmetra", Role.Damage, false), new("Torbjörn", Role.Damage, true),
        new("Tracer", Role.Damage, true), new("Vendetta", Role.Damage, true),
        new("Venture", Role.Damage, false), new("Widowmaker", Role.Damage, false),

        new("Ana", Role.Support, true), new("Baptiste", Role.Support, false),
        new("Brigitte", Role.Support, true), new("Illari", Role.Support, false),
        new("Jetpack Cat", Role.Support, true), new("Juno", Role.Support, true),
        new("Kiriko", Role.Support, true), new("Lifeweaver", Role.Support, false),
        new("Lúcio", Role.Support, true), new("Mercy", Role.Support, true),
        new("Mizuki", Role.Support, false), new("Moira", Role.Support, true),
        new("Wuyang", Role.Support, true), new("Zenyatta", Role.Support, true),
    ];

    public static IReadOnlyList<Hero> For(Role role, HeroPool pool) => All
        .Where(hero => hero.Role == role && (pool == HeroPool.Standard || hero.Stadium))
        .ToArray();
}

public static class GameRules
{
    private static readonly Role[] DefaultRoles = [Role.Tank, Role.Damage, Role.Damage, Role.Support, Role.Support];

    public static List<Role> RandomizerRoles(RandomizerMode mode, int customCount, Random random)
    {
        var count = mode switch
        {
            RandomizerMode.FiveVsFive or RandomizerMode.Stadium => 5,
            RandomizerMode.Open => 6,
            _ => Math.Clamp(customCount, 1, 10),
        };
        var roles = mode switch
        {
            RandomizerMode.FiveVsFive or RandomizerMode.Stadium => DefaultRoles.ToList(),
            RandomizerMode.Open =>
            [Role.Tank, Role.Tank, Role.Damage, Role.Damage, Role.Damage, Role.Damage,
             Role.Damage, Role.Damage, Role.Support, Role.Support, Role.Support, Role.Support,
             Role.Support, Role.Support],
            _ => Enumerable.Range(0, count).Select(_ => (Role)random.Next(3)).ToList(),
        };
        Shuffle(roles, random);
        return roles.Take(count).ToList();
    }

    public static List<Hero> HeroesFor(IReadOnlyList<Role> roles, HeroPool pool, Random random)
    {
        var selected = new List<Hero>(roles.Count);
        foreach (var role in roles)
        {
            var available = HeroCatalog.For(role, pool).Where(hero => !selected.Contains(hero)).ToArray();
            selected.Add(available[random.Next(available.Length)]);
        }
        return selected;
    }

    public static List<Hero> GameRoster(HeroPool pool, Random random) =>
        HeroesFor(ShuffleCopy(DefaultRoles, random), pool, random);

    public static int RoundPoints(RoundStats stats) =>
        55 + stats.Eliminations * 12 + stats.ObjectiveSeconds / 10 * 10 +
        stats.Damage / 400 + stats.Healing / 400 - stats.Deaths * 5;

    public static int TransferReceived(int amount) => (amount + 1) / 2;

    public static string RoleName(Role role) => role switch
    {
        Role.Tank => "Танк",
        Role.Damage => "Урон",
        _ => "Поддержка",
    };

    private static List<T> ShuffleCopy<T>(IEnumerable<T> values, Random random)
    {
        var result = values.ToList();
        Shuffle(result, random);
        return result;
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }
}
