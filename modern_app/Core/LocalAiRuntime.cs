using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

namespace OverwatchRandomizer.Modern.Core;

public readonly record struct AiDownloadProgress(double Fraction, double DownloadedMegabytes, double TotalMegabytes);
public enum AiAcceleration { Auto, Gpu, Cpu }
public enum AiModelTier { Standard, Advanced }

public static class LocalAiRuntime
{
    private sealed record ModelSpec(string Bundle, string ModelUrl, string ProjectorUrl, string ModelHash,
        string ProjectorHash, long ModelBytes, long ProjectorBytes);
    private static readonly ModelSpec StandardModel = new("unsloth-0.8b-q4km-v1",
        "https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF/resolve/main/Qwen3.5-0.8B-Q4_K_M.gguf",
        "https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF/resolve/main/mmproj-BF16.gguf",
        "bd258782e35f7f458f8aced1adc053e6e92e89bc735ba3be89d38a06121dc517",
        "d312c4d02fd46eea7a16e4f3bbb58840e6222209322ca1e33ca03247ad8935d6", 532_517_120, 207_346_528);
    private static readonly ModelSpec AdvancedModel = new("unsloth-2b-q4km-v1",
        "https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/Qwen3.5-2B-Q4_K_M.gguf",
        "https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/mmproj-BF16.gguf",
        "aaf42c8b7c3cab2bf3d69c355048d4a0ee9973d48f16c731c0520ee914699223",
        "f17196c0d8fc756bc65be60075bd4a359917eee8a438505639511727c585d3c2", 1_280_835_840, 671_372_992);
    private static readonly HttpClient Downloads = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private static readonly HttpClient Health = new() { Timeout = TimeSpan.FromSeconds(3) };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool running;
#if WINDOWS
    private static Process? serverProcess;
#elif ANDROID
    private static Java.Lang.Process? serverProcess;
#endif

    public static bool Enabled => Preferences.Default.Get("ai_enabled", false);
    public static AiModelTier ModelTier => Enum.TryParse<AiModelTier>(Preferences.Default.Get("ai_model_tier", "Standard"), out var value) ? value : AiModelTier.Standard;
    private static ModelSpec SelectedModel => ModelTier == AiModelTier.Advanced ? AdvancedModel : StandardModel;
    public static AiAcceleration Acceleration => Enum.TryParse<AiAcceleration>(Preferences.Default.Get("ai_acceleration", "Auto"), out var value) ? value : AiAcceleration.Auto;
    public static bool IsInstalled => File.Exists(ModelPath) && File.Exists(ProjectorPath) &&
        Preferences.Default.Get("ai_bundle", "") == SelectedModel.Bundle;
    public static Uri ApiBase { get; } = new("http://127.0.0.1:8080/v1/");
    private static string AiDirectory => Path.Combine(FileSystem.AppDataDirectory, "ai");
    private static string ModelPath => Path.Combine(AiDirectory, "model.gguf");
    private static string ProjectorPath => Path.Combine(AiDirectory, "vision.gguf");

    public static void SetEnabled(bool enabled) => Preferences.Default.Set("ai_enabled", enabled);
    public static void SetModelTier(AiModelTier tier)
    {
        Preferences.Default.Set("ai_model_tier", tier.ToString());
        StopServer();
    }
    public static void SetAcceleration(AiAcceleration acceleration)
    {
        Preferences.Default.Set("ai_acceleration", acceleration.ToString());
        StopServer();
    }

    public static Task InstallAsync(IProgress<AiDownloadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => InstallCoreAsync(progress, cancellationToken), cancellationToken);

    private static async Task InstallCoreAsync(IProgress<AiDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AiDirectory);
        var model = SelectedModel;
        var downloaded = 0L;
        await DownloadVerifiedAsync(model.ModelUrl, ModelPath, model.ModelHash, model.ModelBytes, downloaded, model.ModelBytes + model.ProjectorBytes, progress, cancellationToken);
        downloaded += model.ModelBytes;
        await DownloadVerifiedAsync(model.ProjectorUrl, ProjectorPath, model.ProjectorHash, model.ProjectorBytes, downloaded, model.ModelBytes + model.ProjectorBytes, progress, cancellationToken);
        Preferences.Default.Set("ai_bundle", model.Bundle);
        progress?.Report(new AiDownloadProgress(1, (model.ModelBytes + model.ProjectorBytes) / 1_000_000d,
            (model.ModelBytes + model.ProjectorBytes) / 1_000_000d));
    }

    public static Task EnsureRunningAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => EnsureRunningCoreAsync(cancellationToken), cancellationToken);

    private static async Task EnsureRunningCoreAsync(CancellationToken cancellationToken)
    {
        if (!Enabled) throw new InvalidOperationException("ИИ отключён. Включите его во вкладке ИИ-чата.");
        if (!IsInstalled)
            throw new InvalidOperationException("Файлы ИИ не установлены. Включите ИИ и завершите загрузку.");
        if (running && await IsHealthyAsync()) return;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (running && await IsHealthyAsync()) return;
            var gpuAttempt = StartServer();
            var deadline = DateTime.UtcNow.AddMinutes(3);
            var cpuFallbackUsed = false;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsHealthyAsync()) { running = true; return; }
                if (ServerExited())
                {
                    if (gpuAttempt && Acceleration == AiAcceleration.Auto && !cpuFallbackUsed)
                    {
                        cpuFallbackUsed = true;
                        gpuAttempt = StartServer(forceCpu: true);
                    }
                    else throw new InvalidOperationException("Локальный ИИ не смог запуститься на этом устройстве.");
                }
                await Task.Delay(750, cancellationToken);
            }
            throw new TimeoutException("Локальный ИИ запускается слишком долго.");
        }
        finally { Gate.Release(); }
    }

    private static async Task DownloadVerifiedAsync(string url, string destination, string expectedHash, long estimatedBytes,
        long completedBytes, long total, IProgress<AiDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (File.Exists(destination) && await HasExpectedHash(destination, expectedHash, cancellationToken)) return;
        var partial = destination + ".part";
        if (File.Exists(partial)) File.Delete(partial);
        using var response = await Downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var fileBytes = response.Content.Headers.ContentLength ?? estimatedBytes;
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            var buffer = new byte[1024 * 1024];
            long current = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                current += read;
                var aggregate = Math.Min(total, completedBytes + current * estimatedBytes / Math.Max(1, fileBytes));
                progress?.Report(new AiDownloadProgress(aggregate / (double)total, aggregate / 1_000_000d, total / 1_000_000d));
            }
        }
        if (!await HasExpectedHash(partial, expectedHash, cancellationToken))
        {
            File.Delete(partial);
            throw new InvalidDataException("Проверка загруженного файла ИИ не пройдена. Повторите загрузку.");
        }
        File.Move(partial, destination, true);
    }

    private static async Task<bool> HasExpectedHash(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash) == expected;
    }

    private static async Task<bool> IsHealthyAsync()
    {
        try { return (await Health.GetAsync("http://127.0.0.1:8080/health")).IsSuccessStatusCode; }
        catch { return false; }
    }

    private static bool StartServer(bool forceCpu = false)
    {
        if (!ServerExited()) return false;
        var arguments = new List<string>
        {
            "--model", ModelPath, "--mmproj", ProjectorPath, "--host", "127.0.0.1", "--port", "8080",
            "--ctx-size", "2048", "--batch-size", "128", "--ubatch-size", "128", "--parallel", "1",
            "--jinja", "--reasoning", "off", "--no-webui",
        };
#if WINDOWS
        var gpu = !forceCpu && Acceleration != AiAcceleration.Cpu && Directory.Exists(Path.Combine(AppContext.BaseDirectory, "ai-runtime-gpu"));
        var runtimeDirectory = Path.Combine(AppContext.BaseDirectory, gpu ? "ai-runtime-gpu" : "ai-runtime");
        if (gpu) { arguments.Add("--gpu-layers"); arguments.Add("99"); }
        var start = new ProcessStartInfo(Path.Combine(runtimeDirectory, "llama-server.exe"))
        {
            WorkingDirectory = runtimeDirectory, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        serverProcess = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить локальный ИИ.");
        return gpu;
#elif ANDROID
        var runtimeDirectory = Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir
            ?? throw new InvalidOperationException("Каталог локального ИИ недоступен.");
        var command = new[] { Path.Combine(runtimeDirectory, "libllamaserver.so") }.Concat(arguments).ToArray();
        var builder = new Java.Lang.ProcessBuilder(command);
        builder.Directory(new Java.IO.File(runtimeDirectory));
        var environment = builder.Environment();
        if (environment is not null) environment["LD_LIBRARY_PATH"] = runtimeDirectory;
        builder.RedirectErrorStream(true);
        serverProcess = builder.Start();
        var process = serverProcess ?? throw new InvalidOperationException("Не удалось запустить локальный ИИ.");
        _ = Task.Run(() =>
        {
            var buffer = new byte[4096];
            try
            {
                var output = process.InputStream;
                if (output is not null) while (output.Read(buffer) > 0) { }
            }
            catch { }
        });
        return false;
#else
        throw new PlatformNotSupportedException("Локальный ИИ не поддерживается на этой платформе.");
#endif
    }

    private static void StopServer()
    {
#if WINDOWS
        try { if (serverProcess is { HasExited: false }) serverProcess.Kill(true); } catch { }
        serverProcess?.Dispose();
        serverProcess = null;
#elif ANDROID
        try { serverProcess?.Destroy(); } catch { }
        serverProcess?.Dispose();
        serverProcess = null;
#endif
        running = false;
    }

    private static bool ServerExited()
    {
#if WINDOWS
        return serverProcess is null || serverProcess.HasExited;
#elif ANDROID
        if (serverProcess is null) return true;
        try { _ = serverProcess.ExitValue(); return true; } catch (Java.Lang.IllegalThreadStateException) { return false; }
#else
        return true;
#endif
    }
}
