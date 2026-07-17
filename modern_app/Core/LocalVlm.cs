using System.Net.Http.Json;
using System.Text.Json;

namespace OverwatchRandomizer.Modern.Core;

public static class LocalVlm
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static async Task<IReadOnlyList<string>> ReadEnemyTeamAsync(FileResult file)
    {
        var schema = new
        {
            type = "object",
            properties = new { heroes = new { type = "array", items = new { type = "string", @enum = HeroCatalog.All.Select(hero => hero.Name).ToArray() }, minItems = 1, maxItems = 6 } },
            required = new[] { "heroes" },
            additionalProperties = false,
        };
        using var payload = await ImagePayload(file,
            "This is an Overwatch scoreboard. Find the RED enemy team table, normally in the lower half. Read exactly the small hero portrait at the far LEFT of each red row, from top to bottom. The BLUE/CYAN upper rows are friendly teammates: ignore every blue row. Ignore the large selected-hero portrait and statistics panel on the right. Identify heroes visually from their portraits; player names are not hero names. Return only exact hero names from the allowed enum in red-row order.",
            "enemy_team", schema, 128);
        var content = await Complete(payload);
        return JsonDocument.Parse(content).RootElement.GetProperty("heroes").EnumerateArray()
            .Select(item => item.GetString()).Where(item => item is not null).Cast<string>().Distinct().ToArray();
    }

    public static async Task<Dictionary<int, RoundStats>> ReadStatsAsync(FileResult file, IReadOnlyList<string> playerNames)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                players = new
                {
                    type = "array", minItems = 0, maxItems = playerNames.Count,
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            index = new { type = "integer", minimum = 0, maximum = Math.Max(0, playerNames.Count - 1) },
                            eliminations = new { type = "integer", minimum = 0 }, objective_seconds = new { type = "integer", minimum = 0 },
                            damage = new { type = "integer", minimum = 0 }, healing = new { type = "integer", minimum = 0 }, deaths = new { type = "integer", minimum = 0 },
                        },
                        required = new[] { "index", "eliminations", "objective_seconds", "damage", "healing", "deaths" },
                        additionalProperties = false,
                    },
                },
            },
            required = new[] { "players" }, additionalProperties = false,
        };
        using var payload = await ImagePayload(file,
            $"Read the scoreboard rows for these friendly players in this exact order: {string.Join(", ", playerNames.Select((name, index) => $"{index}={name}"))}. Objective time must be integer seconds. Omit rows you cannot identify.",
            "scoreboard", schema);
        var content = await Complete(payload);
        return JsonDocument.Parse(content).RootElement.GetProperty("players").EnumerateArray().ToDictionary(
            item => item.GetProperty("index").GetInt32(), item => new RoundStats
            {
                Eliminations = item.GetProperty("eliminations").GetInt32(),
                ObjectiveSeconds = item.GetProperty("objective_seconds").GetInt32(),
                Damage = item.GetProperty("damage").GetInt32(), Healing = item.GetProperty("healing").GetInt32(),
                Deaths = item.GetProperty("deaths").GetInt32(),
            });
    }

    public static async Task<string> ChatAsync(IReadOnlyList<(string Role, string Text)> history)
    {
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            model = "local", temperature = 0.2, max_tokens = 512,
            chat_template_kwargs = new { enable_thinking = false },
            messages = history.Select(item => new { role = item.Role, content = item.Text }).ToArray(),
        });
        return await Complete(payload);
    }

    private static async Task<JsonDocument> ImagePayload(FileResult file, string prompt, string schemaName, object schema, int maxTokens = 512)
    {
        await using var stream = await file.OpenReadAsync();
        if (stream.CanSeek && stream.Length > 20 * 1024 * 1024) throw new InvalidDataException("Image is larger than 20 MB");
        using var memory = new MemoryStream();
        var mime = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType;
#if ANDROID
        var extension = Path.GetExtension(file.FileName);
        if (mime.Contains("heic", StringComparison.OrdinalIgnoreCase) || mime.Contains("heif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".heic", StringComparison.OrdinalIgnoreCase) || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
        {
            using var bitmap = Android.Graphics.BitmapFactory.DecodeStream(stream)
                ?? throw new InvalidDataException("HEIC/HEIF image could not be decoded");
            if (!bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 92, memory))
                throw new InvalidDataException("HEIC/HEIF image could not be converted");
            mime = "image/jpeg";
        }
        else await stream.CopyToAsync(memory);
#else
        await stream.CopyToAsync(memory);
#endif
        return JsonSerializer.SerializeToDocument(new
        {
            model = "local", temperature = 0, max_tokens = maxTokens,
            chat_template_kwargs = new { enable_thinking = false },
            messages = new[] { new { role = "user", content = (object)new object[]
            {
                new { type = "text", text = prompt },
                new { type = "image_url", image_url = new { url = $"data:{mime};base64,{Convert.ToBase64String(memory.ToArray())}" } },
            } } },
            response_format = new { type = "json_schema", json_schema = new { name = schemaName, strict = true, schema } },
        });
    }

    private static async Task<string> Complete(JsonDocument payload)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await LocalAiRuntime.EnsureRunningAsync();
            try
            {
                using var response = await Http.PostAsJsonAsync(new Uri(LocalAiRuntime.ApiBase, "chat/completions"), payload.RootElement);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"VLM {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
                using var json = JsonDocument.Parse(body);
                return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                    ?? throw new InvalidDataException("VLM returned empty content");
            }
            catch (HttpRequestException) when (attempt == 0) { await Task.Delay(500); }
        }
        throw new InvalidOperationException("Локальный ИИ закрыл соединение. Освободите память устройства и повторите попытку.");
    }
}
