using OverwatchRandomizer.Modern.Core;
using System.Text.Json;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Check(HeroCatalog.All.Count == 53 && HeroCatalog.All.Select(hero => hero.Name).Distinct().Count() == 53, "hero count and uniqueness");
Check(HeroCatalog.All.Count(hero => hero.Stadium) == 33, "stadium count");
var dmon = HeroCatalog.All.Single(hero => hero.Name == "D.Mon");
Check(dmon.Role == Role.Tank && !dmon.Stadium && dmon.ImageName == "hero_dmon.png", "D.Mon role, pool and portrait");
Check(HeroCatalog.All.Single(hero => hero.Name == "Sombra").Role == Role.Damage &&
      HeroCatalog.All.All(hero => hero.Name != "Doctrine"), "unreleased Season 5 changes stay out of the live roster");
Check(HeroCatalog.All.All(hero => File.Exists(Path.Combine(AppContext.BaseDirectory, "portraits", hero.ImageName))), "all hero portraits are packaged");
foreach (var rank in new[] { "bronze", "silver", "gold", "platinum", "diamond", "master", "grandmaster" })
{
    var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "counterpickgg", $"counterpicks_pc_competitive_{rank}.json"));
    using var snapshot = JsonDocument.Parse(json);
    var heroes = snapshot.RootElement.GetProperty("heroes");
    Check(snapshot.RootElement.GetProperty("rank").GetString() == rank &&
          heroes.EnumerateObject().Select(hero => hero.Name).ToHashSet().SetEquals(HeroCatalog.All.Select(hero => hero.Name)), $"{rank}: complete roster");
    foreach (var hero in HeroCatalog.All)
    {
        var data = heroes.GetProperty(hero.Name);
        Check(data.GetProperty("role").GetString() == hero.Role.ToString().ToLowerInvariant(), $"{rank}: {hero.Name} role");
        foreach (var key in new[] { "countered_by", "counters" })
            Check(data.GetProperty(key).GetArrayLength() > 0 && data.GetProperty(key).EnumerateArray().All(counter =>
                heroes.TryGetProperty(counter.GetProperty("hero").GetString()!, out _) &&
                counter.GetProperty("strength").GetInt32() is >= 1 and <= 10), $"{rank}: {hero.Name} {key}");
    }
    var dmonTeams = CounterpickEngine.Calculate(json, [dmon.Name], CounterpickMode.FiveVsFive);
    Check(dmonTeams.Count == 5 && dmonTeams[0].Score > 0, $"{rank}: D.Mon has counterpick data");
}
var session = new GameSession(new Random(7));
session.Start(5, HeroPool.Standard);
Check(session.Players.All(player => player.Choices.Count == 5 && player.Choices.Distinct().Count() == 5), "unique choices");
Check(session.Players.All(player => player.Choices.Select(hero => hero.Role).Order().SequenceEqual(
    new[] { Role.Tank, Role.Damage, Role.Damage, Role.Support, Role.Support }.Order())), "1-2-2");
session.Start(5, HeroPool.Stadium);
Check(session.Players.SelectMany(player => player.Choices).All(hero => hero.Stadium), "stadium pool");
var player = session.Players[0];
player.Points = 500;
var exact = HeroCatalog.All.First(hero => hero.Stadium && !player.Choices.Contains(hero));
Check(session.BuyExact(0, exact).Success && player.Points == 360 && player.Selected == exact, "exact purchase");
Check(session.BuyRole(0).Success && player.Points == 275, "role purchase");
Check(session.BuyFull(0).Success && player.Points == 225, "full purchase");
session.Players[0].Points = 51;
Check(session.Transfer(0, 1, 51).Success && session.Players[1].Points == 26, "transfer rounding");
Check(GameRules.TransferReceived(50) == 25, "even transfer");
Check(GameRules.RoundPoints(new RoundStats { Eliminations = 2, ObjectiveSeconds = 25, Damage = 800, Healing = 799, Deaths = 3 }) == 87, "scoring");
Check(session.EmergencyReroll(0).Success && !session.Players[0].EmergencyAvailable && session.Players[1].EmergencyAvailable, "personal emergency use");
session.FinishRound();
Check(session.Players.All(item => item.EmergencyAvailable), "emergency reset");
var parsed = ScoreboardParser.Parse(["Mixlazer 10 1:20 2,400 800 5"], ["Mixlazer"]);
Check(parsed[0].ObjectiveSeconds == 80 && parsed[0].Damage == 2400, "ocr parser");
const string counters = """{"heroes":{"Ana":{"countered_by":[{"hero":"Winston","strength":9},{"hero":"Tracer","strength":8},{"hero":"Kiriko","strength":7}]}}}""";
var teams = CounterpickEngine.Calculate(counters, ["Ana"], CounterpickMode.FiveVsFive);
Check(teams.Count == 5 && teams[0].Picks.Count == 5 && teams[0].Score == 24, "counterpick top 5");
Check(teams[0].Picks.Count(pick => pick.Hero.Role == Role.Tank) == 1 &&
      teams[0].Picks.Count(pick => pick.Hero.Role == Role.Damage) == 2 &&
      teams[0].Picks.Count(pick => pick.Hero.Role == Role.Support) == 2, "counterpick 1-2-2");
Check(teams.SelectMany(team => team.Picks).Select(pick => pick.Hero.Name).Distinct().Count() ==
      teams.SelectMany(team => team.Picks).Count(), "top 5 heroes do not repeat");
Check(CounterpickEngine.Calculate(counters, ["Ana"], CounterpickMode.Open)[0].Picks.Count == 6, "counterpick open 6v6");
Check(CounterpickEngine.MatchHeroes("D.Va, D.Mon; dmon; Genji; unknown").SequenceEqual(["D.Va", "D.Mon", "Genji"]), "manual enemy parsing");
Console.WriteLine("OK: heroes, game rules, scoring, transfer, VLM parser contract, deterministic counterpicks");
