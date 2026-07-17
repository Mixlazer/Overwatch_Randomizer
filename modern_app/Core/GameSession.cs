namespace OverwatchRandomizer.Modern.Core;

public sealed class RoundStats
{
    public int Eliminations { get; set; }
    public int ObjectiveSeconds { get; set; }
    public int Damage { get; set; }
    public int Healing { get; set; }
    public int Deaths { get; set; }
}

public sealed class PlayerState
{
    public required string Name { get; set; }
    public int Points { get; set; }
    public required List<Hero> Choices { get; set; }
    public required Hero Selected { get; set; }
    public RoundStats Stats { get; set; } = new();
    public bool EmergencyAvailable { get; set; } = true;
}

public readonly record struct ActionResult(bool Success, string Message)
{
    public static ActionResult Ok(string message) => new(true, message);
    public static ActionResult Fail(string message) => new(false, message);
}

public sealed class GameSession
{
    private readonly Random random;

    public GameSession(Random? random = null) => this.random = random ?? Random.Shared;

    public List<PlayerState> Players { get; } = [];
    public HeroPool Pool { get; private set; } = HeroPool.Standard;

    public void Start(int count, HeroPool pool, IReadOnlyList<string>? previousNames = null)
    {
        count = Math.Clamp(count, 1, 10);
        Pool = pool;
        Players.Clear();
        for (var index = 0; index < count; index++)
        {
            var choices = GameRules.GameRoster(pool, random);
            Players.Add(new PlayerState
            {
                Name = previousNames?.ElementAtOrDefault(index) ?? $"Игрок {index + 1}",
                Choices = choices,
                Selected = choices[0],
            });
        }
    }

    public ActionResult Select(int playerIndex, Hero hero)
    {
        var player = Players[playerIndex];
        if (!player.Choices.Contains(hero)) return ActionResult.Fail("Герой не входит в личный набор");
        player.Selected = hero;
        return ActionResult.Ok(hero.Name);
    }

    public ActionResult BuyExact(int playerIndex, Hero hero)
    {
        var player = Players[playerIndex];
        if (Pool == HeroPool.Stadium && !hero.Stadium) return ActionResult.Fail("Герой недоступен на Стадионе");
        if (player.Choices.Contains(hero))
        {
            player.Selected = hero;
            return ActionResult.Ok("Герой уже доступен");
        }
        if (player.Points < 140) return ActionResult.Fail("Нужно 140 очков");
        player.Points -= 140;
        player.Choices[player.Choices.IndexOf(player.Selected)] = hero;
        player.Selected = hero;
        return ActionResult.Ok("Точный герой: −140");
    }

    public ActionResult BuyRole(int playerIndex)
    {
        var player = Players[playerIndex];
        if (player.Points < 85) return ActionResult.Fail("Нужно 85 очков");
        player.Points -= 85;
        var role = player.Selected.Role;
        var slots = player.Choices.Select((hero, index) => (hero, index))
            .Where(item => item.hero.Role == role).Select(item => item.index).ToArray();
        var old = slots.Select(slot => player.Choices[slot]).ToArray();
        List<Hero> replacement;
        var attempts = 0;
        do replacement = Sample(HeroCatalog.For(role, Pool), slots.Length);
        while (++attempts < 10 && replacement.SequenceEqual(old));
        for (var index = 0; index < slots.Length; index++) player.Choices[slots[index]] = replacement[index];
        player.Selected = replacement[0];
        return ActionResult.Ok($"{GameRules.RoleName(role)}: −85");
    }

    public ActionResult BuyFull(int playerIndex)
    {
        var player = Players[playerIndex];
        if (player.Points < 50) return ActionResult.Fail("Нужно 50 очков");
        player.Points -= 50;
        player.Choices = GameRules.GameRoster(Pool, random);
        player.Selected = player.Choices[0];
        return ActionResult.Ok("Полный реролл: −50");
    }

    public ActionResult EmergencyReroll(int playerIndex)
    {
        var player = Players[playerIndex];
        if (!player.EmergencyAvailable) return ActionResult.Fail("Аварийный реролл этого игрока уже использован");
        player.Choices = GameRules.GameRoster(Pool, random);
        player.Selected = player.Choices[0];
        player.EmergencyAvailable = false;
        return ActionResult.Ok($"{player.Name}: все пять героев обновлены");
    }

    public ActionResult Transfer(int from, int to, int amount)
    {
        if (from == to || to < 0 || to >= Players.Count) return ActionResult.Fail("Выберите другого игрока");
        if (amount <= 0) return ActionResult.Fail("Введите положительное целое число");
        if (Players[from].Points < amount) return ActionResult.Fail("Недостаточно очков");
        Players[from].Points -= amount;
        var received = GameRules.TransferReceived(amount);
        Players[to].Points += received;
        return ActionResult.Ok($"Передано {amount}, получено {received}");
    }

    public IReadOnlyList<int> FinishRound()
    {
        var gains = Players.Select(player => GameRules.RoundPoints(player.Stats)).ToArray();
        for (var index = 0; index < Players.Count; index++)
        {
            Players[index].Points += gains[index];
            Players[index].Stats = new RoundStats();
            Players[index].EmergencyAvailable = true;
        }
        return gains;
    }

    private List<Hero> Sample(IReadOnlyList<Hero> source, int count)
    {
        var values = source.ToList();
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
        return values.Take(count).ToList();
    }
}
