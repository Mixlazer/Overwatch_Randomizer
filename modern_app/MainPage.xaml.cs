using OverwatchRandomizer.Modern.Core;

namespace OverwatchRandomizer.Modern;

public partial class MainPage : ContentPage
{
    private enum AppTab { Randomizer, Game, Counterpicks, Chat }
    private static readonly Color Accent = Color.FromArgb("#FF8A16");
    private static readonly Color Panel = Color.FromArgb("#111820");
    private static readonly Color Raised = Color.FromArgb("#18212B");
    private static readonly Color Stroke = Color.FromArgb("#2B3743");
    private static readonly Color Muted = Color.FromArgb("#93A0AD");
    private readonly Random random = new();
    private readonly GameSession session = new();
    private readonly List<string[]> statValues = [];
    private RandomizerMode randomizerMode = RandomizerMode.FiveVsFive;
    private int customCount = 5;
    private List<Role> generatedRoles = [];
    private List<Hero> generatedHeroes = [];
    private AppTab activeTab;
    private bool english;
    private string counterRank = "gold";
    private CounterpickMode counterMode;
    private readonly string?[] counterEnemySelections = new string?[6];
    private List<string> counterEnemies = [];
    private IReadOnlyList<CounterpickTeam> counterTeams = [];
    private readonly List<(string Role, string Text)> chatHistory =
        [("system", "You are a concise Overwatch assistant. Answer in the language used by the user.")];
    private int expandedPlayer = -1;
    private int activePlayer;
    private bool? lastMobile;
    private Label? statusLabel;
    private bool onboardingChecked;

    public MainPage()
    {
        InitializeComponent();
        english = Preferences.Default.Get("language", "ru") == "en";
        LanguagePicker.ItemsSource = new[] { "RU", "EN" };
        LanguagePicker.SelectedIndex = english ? 1 : 0;
        AiLanguagePicker.ItemsSource = new[] { "RU", "EN" };
        AiLanguagePicker.SelectedIndex = english ? 1 : 0;
        session.Start(5, HeroPool.Standard);
        ResetStatValues();
        generatedRoles = GameRules.RandomizerRoles(randomizerMode, customCount, random);
        generatedHeroes = GameRules.HeroesFor(generatedRoles, HeroPool.Standard, random);
        RefreshChrome();
        BuildRandomizer();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (onboardingChecked) return;
        onboardingChecked = true;
        if (!Preferences.Default.ContainsKey("ai_model_choice_v1") || LocalAiRuntime.Enabled && !LocalAiRuntime.IsInstalled) ShowAiSetup();
    }

    private void ShowAiSetup()
    {
        AiSetupTitle.Text = Tr("Включить функции ИИ?", "Enable AI features?");
        AiSetupDescription.Text = Tr(
            "Выберите локальную ИИ-модель. Обычная Q4_K_M скачивает около 740 МБ. Продвинутая 2B Q4_K_M — около 1,95 ГБ. Рекомендуется Wi-Fi. Данные не отправляются в облако.",
            "Choose a local AI model. Standard Q4_K_M downloads about 740 MB. Advanced 2B Q4_K_M is about 1.95 GB. Wi-Fi is recommended. Data is not sent to the cloud.");
        AiManualButton.Text = Tr("Нет, буду вводить вручную", "No, manual input");
        AiEnableButton.Text = Tr("Обычный ИИ — Q4_K_M", "Standard AI — Q4_K_M");
        AiAdvancedButton.Text = Tr("Продвинутый ИИ 2B — Q4_K_M", "Advanced 2B AI — Q4_K_M");
        AiAdvancedWarning.Text = Tr(
            "Продвинутая модель будет иметь хорошую производительность только на топовом железе.",
            "The advanced model performs well only on high-end hardware.");
        if (AiLanguagePicker.SelectedIndex != (english ? 1 : 0)) AiLanguagePicker.SelectedIndex = english ? 1 : 0;
        AiSetupProgress.IsVisible = AiSetupProgressLabel.IsVisible = false;
        AiSetupOverlay.IsVisible = true;
    }

    private void ChangeOnboardingLanguage(object? sender, EventArgs e)
    {
        if (AiLanguagePicker.SelectedIndex < 0) return;
        english = AiLanguagePicker.SelectedIndex == 1;
        Preferences.Default.Set("language", english ? "en" : "ru");
        LanguagePicker.SelectedIndex = english ? 1 : 0;
        ShowAiSetup();
        RefreshChrome();
    }

    private void ChooseManualAi(object? sender, EventArgs e)
    {
        LocalAiRuntime.SetEnabled(false);
        Preferences.Default.Set("ai_choice_set", true);
        Preferences.Default.Set("ai_model_choice_v1", true);
        AiSetupOverlay.IsVisible = false;
        RefreshChrome();
        BuildActiveTab();
    }

    private async void ChooseEnableAi(object? sender, EventArgs e) => await EnableAi(AiModelTier.Standard);

    private async void ChooseAdvancedAi(object? sender, EventArgs e) => await EnableAi(AiModelTier.Advanced);

    private async Task EnableAi(AiModelTier tier)
    {
        AiManualButton.IsEnabled = AiEnableButton.IsEnabled = AiAdvancedButton.IsEnabled = false;
        AiLanguagePicker.IsEnabled = false;
        AiSetupProgress.IsVisible = AiSetupProgressLabel.IsVisible = true;
        AiSetupProgressLabel.Text = Tr("Подготовка загрузки…", "Preparing download…");
        try
        {
            LocalAiRuntime.SetModelTier(tier);
            var progress = new Progress<AiDownloadProgress>(value =>
            {
                AiSetupProgress.Progress = value.Fraction;
                AiSetupProgressLabel.Text = $"{Tr("Загрузка модели", "Downloading model")}: {value.Fraction:P0}  ({value.DownloadedMegabytes:0} / {value.TotalMegabytes:0} MB)";
            });
            await LocalAiRuntime.InstallAsync(progress);
            AiSetupProgressLabel.Text = Tr("Запуск локального ИИ…", "Starting local AI…");
            LocalAiRuntime.SetEnabled(true);
            await LocalAiRuntime.EnsureRunningAsync();
            Preferences.Default.Set("ai_choice_set", true);
            Preferences.Default.Set("ai_model_choice_v1", true);
            AiSetupOverlay.IsVisible = false;
            RefreshChrome();
            BuildActiveTab();
        }
        catch (Exception error)
        {
            LocalAiRuntime.SetEnabled(false);
            AiSetupProgressLabel.Text = Tr($"Не удалось установить ИИ: {error.Message}", $"Could not install AI: {error.Message}");
        }
        finally
        {
            AiManualButton.IsEnabled = AiEnableButton.IsEnabled = AiAdvancedButton.IsEnabled = true;
            AiLanguagePicker.IsEnabled = true;
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        var mobile = IsMobile(width);
        if (lastMobile is null) lastMobile = mobile;
        else if (lastMobile != mobile)
        {
            lastMobile = mobile;
            BuildActiveTab();
        }
        GameFooter.ColumnDefinitions[2].Width = mobile ? new GridLength(1.2, GridUnitType.Star) : new GridLength(1.3, GridUnitType.Star);
        EmergencyButton.FontSize = ScreenshotButton.FontSize = mobile ? 11 : 14;
        TitleLabel.Text = mobile ? "OW Randomizer" : "Overwatch Randomizer";
    }

    private bool IsMobile(double? width = null) => DeviceInfo.Idiom == DeviceIdiom.Phone || (width ?? Width) < 760;

    private async void ShowRandomizer(object? sender, EventArgs e)
    {
        await ShowTab(AppTab.Randomizer, BuildRandomizer);
    }

    private async void ShowGame(object? sender, EventArgs e)
    {
        await ShowTab(AppTab.Game, BuildGame);
    }

    private async void ShowCounterpicks(object? sender, EventArgs e) =>
        await ShowTab(AppTab.Counterpicks, BuildCounterpicks);

    private async void ShowChat(object? sender, EventArgs e) =>
        await ShowTab(AppTab.Chat, BuildChat);

    private async Task ShowTab(AppTab tab, Action builder)
    {
        if (activeTab == tab) return;
        activeTab = tab;
        RefreshChrome();
        await SwapContent(builder);
    }

    private void BuildActiveTab()
    {
        switch (activeTab)
        {
            case AppTab.Game: BuildGame(); break;
            case AppTab.Counterpicks: BuildCounterpicks(); break;
            case AppTab.Chat: BuildChat(); break;
            default: BuildRandomizer(); break;
        }
    }

    private void ChangeLanguage(object? sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex < 0) return;
        var selectedEnglish = LanguagePicker.SelectedIndex == 1;
        if (english == selectedEnglish) return;
        english = selectedEnglish;
        Preferences.Default.Set("language", english ? "en" : "ru");
        RefreshChrome();
        BuildActiveTab();
    }

    private string Tr(string russian, string englishText) => english ? englishText : russian;

    private void RefreshChrome()
    {
        if (LanguagePicker.SelectedIndex != (english ? 1 : 0)) LanguagePicker.SelectedIndex = english ? 1 : 0;
        RandomizerTabButton.Text = Tr("Рандом", "Random");
        GameTabButton.Text = Tr("Игра", "Game");
        CounterTabButton.Text = Tr("Контры", "Counters");
        ChatTabButton.Text = Tr("ИИ-чат", "AI chat");
        RandomizerFooter.IsVisible = activeTab == AppTab.Randomizer;
        GameFooter.IsVisible = activeTab == AppTab.Game;
        CounterFooter.IsVisible = activeTab == AppTab.Counterpicks;
        ChatFooter.IsVisible = activeTab == AppTab.Chat;
        ScreenshotButton.Text = Tr("▣  Скриншот", "▣  Screenshot");
        RolesButton.Text = Tr("↻  Роли", "↻  Roles");
        HeroesButton.Text = Tr("◆  Персонажи", "◆  Heroes");
        FinishRoundButton.Text = Tr("Новый раунд", "New round");
        EnemyScreenshotButton.Text = ScreenshotButton.Text;
        CalculateCounterButton.Text = Tr("◆  Рассчитать", "◆  Calculate");
        ChatEntry.Placeholder = Tr("Сообщение локальной модели…", "Message the local model…");
        ChatSendButton.Text = Tr("Отправить", "Send");
        var aiEnabled = LocalAiRuntime.Enabled;
        ScreenshotButton.IsEnabled = aiEnabled;
        EnemyScreenshotButton.IsEnabled = aiEnabled;
        ChatEntry.IsEnabled = ChatSendButton.IsEnabled = aiEnabled;
    }

    private async Task SwapContent(Action builder)
    {
        if (Motion.Enabled) await ContentStack.FadeToAsync(0, 80, Easing.CubicIn);
        builder();
        ContentStack.Opacity = Motion.Enabled ? 0 : 1;
        ContentStack.TranslationX = Motion.Enabled ? 10 : 0;
        if (Motion.Enabled)
            await Task.WhenAll(ContentStack.FadeToAsync(1, Motion.Duration, Easing.CubicOut),
                ContentStack.TranslateToAsync(0, 0, Motion.Duration, Easing.CubicOut));
    }

    private void SetTabState()
    {
        var buttons = new[] { RandomizerTabButton, GameTabButton, CounterTabButton, ChatTabButton };
        for (var index = 0; index < buttons.Length; index++)
        {
            var selected = index == (int)activeTab;
            buttons[index].BackgroundColor = selected ? Raised : Panel;
            buttons[index].BorderColor = selected ? Accent : Stroke;
        }
    }

    private void BuildRandomizer()
    {
        ContentStack.Clear();
        SetTabState();
        var setup = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 10,
        };
        var modePicker = new Picker
        {
            Title = Tr("Режим", "Mode"),
            ItemsSource = new[] { "5v5", Tr("Открытая", "Open"), Tr("Стадион", "Stadium"), Tr("Своя игра", "Custom") },
            SelectedIndex = (int)randomizerMode,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
        };
        modePicker.SelectedItem = modePicker.ItemsSource.Cast<string>().ElementAt((int)randomizerMode);
        modePicker.SelectedIndexChanged += (_, _) =>
        {
            if (modePicker.SelectedIndex < 0) return;
            randomizerMode = (RandomizerMode)modePicker.SelectedIndex;
            generatedRoles = GameRules.RandomizerRoles(randomizerMode, customCount, random);
            generatedHeroes = GameRules.HeroesFor(generatedRoles, ActiveRandomizerPool(), random);
            BuildRandomizer();
        };
        setup.Add(PickerBorder(modePicker));
        if (randomizerMode == RandomizerMode.Custom)
        {
            var count = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            var label = new Label { Text = customCount.ToString(), WidthRequest = 24, VerticalTextAlignment = TextAlignment.Center };
            var stepper = new Stepper { Minimum = 1, Maximum = 10, Value = customCount, Increment = 1 };
            stepper.ValueChanged += (_, args) =>
            {
                customCount = (int)args.NewValue;
                generatedRoles = GameRules.RandomizerRoles(randomizerMode, customCount, random);
                generatedHeroes = GameRules.HeroesFor(generatedRoles, ActiveRandomizerPool(), random);
                BuildRandomizer();
            };
            count.Add(new Label { Text = Tr("Игроки", "Players"), TextColor = Muted, VerticalTextAlignment = TextAlignment.Center });
            count.Add(label);
            count.Add(stepper);
            setup.Add(count, 1);
        }
        ContentStack.Add(setup);
        ContentStack.Add(new Label
        {
            Text = randomizerMode == RandomizerMode.Stadium ? Tr("Пул: Стадион", "Pool: Stadium") : Tr("Пул: обычная игра", "Pool: standard"),
            TextColor = Muted,
            FontSize = 12,
        });
        for (var index = 0; index < generatedRoles.Count; index++)
            ContentStack.Add(BuildRandomizerRow(index));
    }

    private View BuildRandomizerRow(int index)
    {
        var role = generatedRoles[index];
        var hero = generatedHeroes.ElementAtOrDefault(index);
        var grid = new Grid
        {
            Padding = IsMobile() ? new Thickness(12, 8) : new Thickness(18, 7),
            ColumnDefinitions =
            {
                new ColumnDefinition(48), new ColumnDefinition(IsMobile() ? 42 : 120),
                new ColumnDefinition(66), new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 10,
            HeightRequest = IsMobile() ? 78 : 76,
        };
        grid.Add(new Label { Text = (index + 1).ToString(), FontSize = 23, TextColor = Muted, VerticalTextAlignment = TextAlignment.Center });
        var roleLabel = new Label
        {
            Text = IsMobile() ? RoleSymbol(role) : $"{RoleSymbol(role)}  {RoleName(role)}",
            TextColor = RoleColor(role), FontFamily = "OpenSansSemibold", VerticalTextAlignment = TextAlignment.Center,
        };
        grid.Add(roleLabel, 1);
        grid.Add(new Image { Source = hero?.ImageName, WidthRequest = 56, HeightRequest = 56, Aspect = Aspect.AspectFit }, 2);
        grid.Add(new Label
        {
            Text = hero?.Name ?? "—", FontSize = IsMobile() ? 18 : 20, FontFamily = "OpenSansSemibold",
            VerticalTextAlignment = TextAlignment.Center,
        }, 3);
        return new Border { Content = grid };
    }

    private void GenerateRoles(object? sender, EventArgs e)
    {
        generatedRoles = GameRules.RandomizerRoles(randomizerMode, customCount, random);
        BuildRandomizer();
    }

    private void GenerateHeroes(object? sender, EventArgs e)
    {
        if (generatedRoles.Count == 0) generatedRoles = GameRules.RandomizerRoles(randomizerMode, customCount, random);
        generatedHeroes = GameRules.HeroesFor(generatedRoles, ActiveRandomizerPool(), random);
        BuildRandomizer();
    }

    private HeroPool ActiveRandomizerPool() => randomizerMode == RandomizerMode.Stadium ? HeroPool.Stadium : HeroPool.Standard;

    private void BuildCounterpicks()
    {
        ContentStack.Clear();
        SetTabState();
        var setup = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        var ranks = new[] { "bronze", "silver", "gold", "platinum", "diamond", "master", "grandmaster" };
        var rankNames = english
            ? new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master", "Grandmaster" }
            : new[] { "Бронза", "Серебро", "Золото", "Платина", "Алмаз", "Мастер", "Грандмастер" };
        var rank = new Picker { ItemsSource = rankNames, SelectedIndex = Math.Max(0, Array.IndexOf(ranks, counterRank)) };
        rank.SelectedIndexChanged += (_, _) => counterRank = ranks[Math.Max(0, rank.SelectedIndex)];
        var mode = new Picker
        {
            Title = Tr("Режим", "Mode"), ItemsSource = new[] { "5v5", "Open 6v6", "Stadium" }, SelectedIndex = (int)counterMode,
            BackgroundColor = Colors.Transparent,
        };
        mode.SelectedIndexChanged += (_, _) =>
        {
            var selected = (CounterpickMode)Math.Max(0, mode.SelectedIndex);
            if (selected == counterMode) return;
            counterMode = selected;
            counterTeams = [];
            BuildCounterpicks();
        };
        setup.Add(new VerticalStackLayout { Spacing = 2, Children = { SmallLabel(Tr("Ранг", "Rank")), PickerBorder(rank) } });
        setup.Add(new VerticalStackLayout { Spacing = 2, Children = { SmallLabel(Tr("Режим", "Mode")), PickerBorder(mode) } }, 1);
        ContentStack.Add(setup);
        var enemyGrid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8, RowSpacing = 8,
        };
        var enemyCount = counterMode == CounterpickMode.Open ? 6 : 5;
        var heroOptions = new[] { "—" }.Concat(HeroCatalog.All.Select(hero => hero.Name)).ToArray();
        for (var index = 0; index < enemyCount; index++)
        {
            var enemyIndex = index;
            var picker = new Picker { ItemsSource = heroOptions, SelectedIndex = counterEnemySelections[index] is null ? 0 : Array.IndexOf(heroOptions, counterEnemySelections[index]) };
            picker.SelectedIndexChanged += (_, _) => counterEnemySelections[enemyIndex] = picker.SelectedIndex <= 0 ? null : heroOptions[picker.SelectedIndex];
            var field = new VerticalStackLayout { Spacing = 2, Children = { SmallLabel(Tr($"Враг {index + 1}", $"Enemy {index + 1}")), picker } };
            enemyGrid.Add(field, index % 2, index / 2);
        }
        ContentStack.Add(enemyGrid);
        statusLabel = new Label
        {
            Text = counterEnemies.Count == 0
                ? Tr("Выберите героев или распознайте скриншот.", "Choose heroes or read a screenshot.")
                : Tr($"Распознано: {string.Join(", ", counterEnemies)}", $"Detected: {string.Join(", ", counterEnemies)}"),
            TextColor = Muted, FontSize = 12,
        };
        ContentStack.Add(statusLabel);
        if (counterTeams.Count > 0) ContentStack.Add(new Label
        {
            Text = Tr("Топ-5 составов без повторения героев", "Top 5 teams without repeated heroes"),
            FontFamily = "OpenSansSemibold", FontSize = 16,
        });
        for (var index = 0; index < counterTeams.Count; index++) ContentStack.Add(BuildCounterTeam(index, counterTeams[index]));
    }

    private View BuildCounterTeam(int index, CounterpickTeam team)
    {
        var body = new VerticalStackLayout { Spacing = 7 };
        body.Add(new Label
        {
            Text = $"#{index + 1}  •  {team.Score} {Tr("очков", "points")}", FontFamily = "OpenSansSemibold", TextColor = Accent,
        });
        var heroes = new HorizontalStackLayout { Spacing = 8 };
        foreach (var pick in team.Picks)
        {
            var item = new VerticalStackLayout { Spacing = 2, WidthRequest = IsMobile() ? 58 : 100 };
            item.Add(HeroImage(pick.Hero, IsMobile() ? 52 : 62));
            item.Add(new Label { Text = pick.Hero.Name, FontSize = IsMobile() ? 10 : 12, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation });
            item.Add(new Label { Text = $"+{pick.Score}", FontSize = 10, TextColor = RoleColor(pick.Hero.Role), HorizontalTextAlignment = TextAlignment.Center });
            heroes.Add(item);
        }
        body.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = heroes });
        return new Border { Content = body, Padding = 10 };
    }

    private async void ReadEnemyScreenshot(object? sender, EventArgs e)
    {
        var file = await AcquireScreenshotAsync(Tr("Команда противника", "Enemy team"));
        if (file is null) return;
        EnemyScreenshotButton.IsEnabled = false;
        SetStatus(Tr("VLM распознаёт состав…", "VLM is reading the team…"));
        try
        {
            counterEnemies = (await LocalVlm.ReadEnemyTeamAsync(file)).ToList();
            Array.Clear(counterEnemySelections);
            for (var index = 0; index < Math.Min(counterEnemySelections.Length, counterEnemies.Count); index++) counterEnemySelections[index] = counterEnemies[index];
            BuildCounterpicks();
            SetStatus(Tr($"Распознано: {string.Join(", ", counterEnemies)}", $"Detected: {string.Join(", ", counterEnemies)}"), counterEnemies.Count == 0);
        }
        catch (Exception error) { SetStatus($"VLM: {error.Message}", true); }
        finally { EnemyScreenshotButton.IsEnabled = true; }
    }

    private async void CalculateCounterpicks(object? sender, EventArgs e)
    {
        counterEnemies = counterEnemySelections.Take(counterMode == CounterpickMode.Open ? 6 : 5)
            .Where(name => name is not null).Cast<string>().Distinct().ToList();
        if (counterEnemies.Count == 0)
        {
            SetStatus(Tr("Не найдено ни одного точного имени героя", "No exact hero names found"), true);
            return;
        }
        try
        {
            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync($"counterpicks_pc_competitive_{counterRank}.json");
            using var reader = new StreamReader(stream);
            counterTeams = CounterpickEngine.Calculate(await reader.ReadToEndAsync(), counterEnemies, counterMode);
            BuildCounterpicks();
            SetStatus(Tr($"Топ-5 без повторов для: {string.Join(", ", counterEnemies)}", $"Top 5 without repeats for: {string.Join(", ", counterEnemies)}"));
        }
        catch (Exception error) { SetStatus(Tr($"Расчёт: {error.Message}", $"Calculation: {error.Message}"), true); }
    }

    private void BuildChat()
    {
        ContentStack.Clear();
        SetTabState();
        var acceleration = new Picker
        {
            ItemsSource = new[] { Tr("Авто", "Auto"), "GPU", "CPU" },
            SelectedIndex = (int)LocalAiRuntime.Acceleration,
        };
        acceleration.SelectedIndexChanged += (_, _) =>
        {
            if (acceleration.SelectedIndex < 0) return;
            LocalAiRuntime.SetAcceleration((AiAcceleration)acceleration.SelectedIndex);
        };
        ContentStack.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children = { SmallLabel(Tr("Ускорение ИИ", "AI acceleration")), PickerBorder(acceleration) },
        });
        ContentStack.Add(new Label
        {
            Text = LocalAiRuntime.Enabled
                ? Tr("ИИ работает локально. История хранится только до закрытия приложения.", "AI runs locally. History is kept only until the app closes.")
                : Tr("ИИ отключён. Включите его, чтобы использовать распознавание скриншотов и чат.", "AI is disabled. Enable it to use screenshot recognition and chat."),
            TextColor = Muted, FontSize = 12,
        });
        if (!LocalAiRuntime.Enabled)
        {
            var enable = new Button { Text = Tr("Включить ИИ", "Enable AI"), Style = (Style)Resources["PrimaryButton"] };
            enable.Clicked += (_, _) => ShowAiSetup();
            ContentStack.Add(enable);
            return;
        }
        foreach (var message in chatHistory.Where(item => item.Role != "system"))
        {
            var mine = message.Role == "user";
            ContentStack.Add(new Border
            {
                Content = new Label { Text = message.Text, LineBreakMode = LineBreakMode.WordWrap },
                Padding = 10, BackgroundColor = mine ? Color.FromArgb("#26384A") : Raised,
                HorizontalOptions = mine ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = IsMobile() ? 330 : 720,
            });
        }
    }

    private async void SendChat(object? sender, EventArgs e)
    {
        var text = ChatEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text) || !ChatSendButton.IsEnabled) return;
        chatHistory.Add(("user", text));
        ChatEntry.Text = string.Empty;
        ChatSendButton.IsEnabled = false;
        BuildChat();
        try { chatHistory.Add(("assistant", await LocalVlm.ChatAsync(chatHistory))); }
        catch (Exception error) { chatHistory.Add(("assistant", $"VLM: {error.Message}")); }
        finally { ChatSendButton.IsEnabled = true; BuildChat(); await BodyScroll.ScrollToAsync(ContentStack, ScrollToPosition.End, true); }
    }

    private void BuildGame()
    {
        ContentStack.Clear();
        SetTabState();
        var setup = new Grid
        {
            ColumnDefinitions = IsMobile()
                ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
                : [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(150), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(150), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 8,
        };
        var countPicker = new Picker { ItemsSource = Enumerable.Range(1, 10).Select(value => value.ToString()).ToArray(), SelectedIndex = session.Players.Count - 1 };
        var poolPicker = new Picker
        {
            ItemsSource = new[] { Tr("Обычная", "Standard"), Tr("Стадион", "Stadium") },
            SelectedIndex = session.Pool == HeroPool.Standard ? 0 : 1,
        };
        var apply = new Button { Text = Tr("Применить", "Apply") };
        apply.Clicked += async (_, _) => await StartNewGame(countPicker.SelectedIndex + 1,
            poolPicker.SelectedIndex == 1 ? HeroPool.Stadium : HeroPool.Standard);
        if (IsMobile())
        {
            setup.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            setup.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            setup.Add(SmallLabel(Tr("Игроки", "Players")), 0, 0);
            setup.Add(SmallLabel(Tr("Пул героев", "Hero pool")), 1, 0);
            setup.Add(PickerBorder(countPicker), 0, 1); setup.Add(PickerBorder(poolPicker), 1, 1); setup.Add(apply, 2, 1);
        }
        else
        {
            setup.Add(SmallLabel(Tr("Игроки", "Players"))); setup.Add(PickerBorder(countPicker), 1); setup.Add(SmallLabel(Tr("Пул героев", "Hero pool")), 2);
            setup.Add(PickerBorder(poolPicker), 3); setup.Add(apply, 4);
        }
        ContentStack.Add(setup);
        statusLabel = new Label { Text = session.Pool == HeroPool.Standard ? Tr("Игра: обычный пул", "Game: standard pool") : Tr("Игра: Стадион", "Game: Stadium"), TextColor = Muted, FontSize = 12 };
        ContentStack.Add(statusLabel);
        for (var index = 0; index < session.Players.Count; index++) ContentStack.Add(BuildPlayerCard(index));
        var active = session.Players[Math.Clamp(activePlayer, 0, session.Players.Count - 1)];
        EmergencyButton.Text = $"↻  {active.Name} {(active.EmergencyAvailable ? "1/1" : "0/1")}";
        EmergencyButton.IsEnabled = active.EmergencyAvailable;
    }

    private async Task StartNewGame(int count, HeroPool pool)
    {
        var progressed = session.Players.Any(player => player.Points != 0 || !player.EmergencyAvailable) ||
            statValues.Any(values => values.Any(value => value != "0"));
        if (progressed && !await DisplayAlertAsync(Tr("Новая игра", "New game"), Tr("Сбросить очки, наборы и статистику?", "Reset points, choices and stats?"), Tr("Сбросить", "Reset"), Tr("Отмена", "Cancel"))) return;
        var names = session.Players.Select(player => player.Name).ToArray();
        session.Start(count, pool, names);
        expandedPlayer = 0;
        activePlayer = 0;
        ResetStatValues();
        BuildGame();
    }

    private View BuildPlayerCard(int playerIndex)
    {
        var player = session.Players[playerIndex];
        var mobile = IsMobile();
        var expanded = expandedPlayer == playerIndex;
        var body = new VerticalStackLayout { Spacing = 10 };
        var header = mobile ? BuildMobileHeader(playerIndex, expanded) : BuildDesktopHeader(playerIndex, expanded);
        body.Add(header);
        if (expanded) body.Add(BuildPlayerDetails(playerIndex, includeChoices: mobile));
        var border = new Border { Content = body, Padding = mobile ? new Thickness(10) : new Thickness(10, 7) };
        if (mobile)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                activePlayer = playerIndex;
                expandedPlayer = expanded ? -1 : playerIndex;
                await SwapContent(BuildGame);
            };
            header.GestureRecognizers.Add(tap);
        }
        return border;
    }

    private View BuildMobileHeader(int playerIndex, bool expanded)
    {
        var player = session.Players[playerIndex];
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(58), new ColumnDefinition(34) },
            ColumnSpacing = 8,
        };
        var text = new VerticalStackLayout { Spacing = 1 };
        var name = new Entry { Text = player.Name, FontSize = 18, FontFamily = "OpenSansSemibold" };
        name.TextChanged += (_, args) => player.Name = string.IsNullOrWhiteSpace(args.NewTextValue) ? $"Игрок {playerIndex + 1}" : args.NewTextValue;
        text.Add(name);
        text.Add(new Label { Text = $"{player.Points} {Tr("очк.", "pts")}  •  {RoleName(player.Selected.Role)}  •  {Tr("реролл", "reroll")} {(player.EmergencyAvailable ? "1/1" : "0/1")}", TextColor = RoleColor(player.Selected.Role), FontSize = 12 });
        grid.Add(text);
        grid.Add(HeroImage(player.Selected, 54), 1);
        grid.Add(new Label { Text = expanded ? "⌃" : "⌄", FontSize = 24, TextColor = Muted, VerticalTextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center }, 2);
        return grid;
    }

    private View BuildDesktopHeader(int playerIndex, bool expanded)
    {
        var player = session.Players[playerIndex];
        var grid = new Grid
        {
            WidthRequest = 1100,
            ColumnDefinitions =
            {
                new ColumnDefinition(145), new ColumnDefinition(62), new ColumnDefinition(75),
                new ColumnDefinition(290), new ColumnDefinition(135), new ColumnDefinition(85),
                new ColumnDefinition(85), new ColumnDefinition(85), new ColumnDefinition(34),
            },
            ColumnSpacing = 7,
        };
        var identity = new VerticalStackLayout { Spacing = 0 };
        var name = new Entry { Text = player.Name, FontSize = 14 };
        name.TextChanged += (_, args) => player.Name = string.IsNullOrWhiteSpace(args.NewTextValue) ? $"Игрок {playerIndex + 1}" : args.NewTextValue;
        identity.Add(name);
        identity.Add(new Label { Text = $"{player.Points} {Tr("очков", "points")}  •  {Tr("реролл", "reroll")} {(player.EmergencyAvailable ? "1/1" : "0/1")}", TextColor = Muted, FontSize = 12 });
        grid.Add(identity);
        grid.Add(HeroImage(player.Selected, 54), 1);
        grid.Add(new Label { Text = RoleName(player.Selected.Role), TextColor = RoleColor(player.Selected.Role), FontSize = 12, VerticalTextAlignment = TextAlignment.Center }, 2);
        grid.Add(BuildHeroChoices(playerIndex), 3);
        var exact = BuildExactPicker(playerIndex);
        grid.Add(exact, 4);
        grid.Add(ActionButton(Tr("Точно\n140", "Exact\n140"), () => BuyExact(playerIndex, exact)), 5);
        grid.Add(ActionButton(Tr("Роль\n85", "Role\n85"), () => Buy(playerIndex, () => session.BuyRole(playerIndex))), 6);
        grid.Add(ActionButton(Tr("Все\n50", "All\n50"), () => Buy(playerIndex, () => session.BuyFull(playerIndex))), 7);
        var expand = new Button { Text = expanded ? "⌃" : "⌄", FontSize = 18, Padding = 0, BorderWidth = 0, BackgroundColor = Colors.Transparent };
        expand.Clicked += async (_, _) =>
        {
            activePlayer = playerIndex;
            expandedPlayer = expanded ? -1 : playerIndex;
            await SwapContent(BuildGame);
        };
        grid.Add(expand, 8);
        return new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = grid };
    }

    private View BuildPlayerDetails(int playerIndex, bool includeChoices)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        if (includeChoices)
        {
            content.Add(BuildHeroChoices(playerIndex));
            var exact = BuildExactPicker(playerIndex);
            content.Add(exact);
            var actions = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 6 };
            actions.Add(ActionButton(Tr("Точно 140", "Exact 140"), () => BuyExact(playerIndex, exact)));
            actions.Add(ActionButton(Tr("Роль 85", "Role 85"), () => Buy(playerIndex, () => session.BuyRole(playerIndex))), 1);
            actions.Add(ActionButton(Tr("Все 50", "All 50"), () => Buy(playerIndex, () => session.BuyFull(playerIndex))), 2);
            content.Add(actions);
        }
        content.Add(BuildStats(playerIndex));
        content.Add(BuildTransfer(playerIndex));
        return content;
    }

    private View BuildHeroChoices(int playerIndex)
    {
        var player = session.Players[playerIndex];
        var choices = new HorizontalStackLayout { Spacing = 5 };
        foreach (var hero in player.Choices)
        {
            var button = new ImageButton
            {
                Source = hero.ImageName,
                WidthRequest = IsMobile() ? 58 : 52,
                HeightRequest = IsMobile() ? 58 : 52,
                Padding = 0,
                BackgroundColor = Raised,
                BorderColor = hero == player.Selected ? Accent : Stroke,
                BorderWidth = hero == player.Selected ? 3 : 1,
                CornerRadius = 4,
                Aspect = Aspect.AspectFit,
            };
            button.Clicked += (_, _) =>
            {
                activePlayer = playerIndex;
                session.Select(playerIndex, hero);
                BuildGame();
            };
            choices.Add(button);
        }
        return new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = choices };
    }

    private Picker BuildExactPicker(int playerIndex)
    {
        var allowed = HeroCatalog.All.Where(hero => session.Pool == HeroPool.Standard || hero.Stadium).ToArray();
        var selected = session.Players[playerIndex].Selected;
        return new Picker
        {
            Title = Tr("Все герои", "All heroes"),
            ItemsSource = allowed.Select(hero => hero.Name).ToArray(),
            SelectedItem = selected.Name,
            MinimumWidthRequest = 120,
        };
    }

    private View BuildStats(int playerIndex)
    {
        var labels = english ? new[] { "ELIM", "OBJ", "DMG", "HEAL", "DEATH" } : new[] { "УБ", "ПОГЛ", "УРОН", "ИС", "СОД" };
        var grid = new Grid { ColumnSpacing = 6 };
        for (var index = 0; index < 5; index++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < labels.Length; index++)
        {
            var field = new VerticalStackLayout { Spacing = 2 };
            field.Add(SmallLabel(labels[index]));
            var statIndex = index;
            var entry = new Entry { Text = statValues[playerIndex][index], Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.Center };
            entry.TextChanged += (_, args) => statValues[playerIndex][statIndex] = args.NewTextValue ?? string.Empty;
            field.Add(entry);
            grid.Add(field, index);
        }
        return grid;
    }

    private View BuildTransfer(int playerIndex)
    {
        var targets = session.Players.Select((player, index) => (player, index)).Where(item => item.index != playerIndex).ToArray();
        var picker = new Picker { Title = Tr("Кому передать", "Recipient"), ItemsSource = targets.Select(item => item.player.Name).ToArray(), SelectedIndex = targets.Length > 0 ? 0 : -1 };
        var amount = new Entry { Text = "50", Keyboard = Keyboard.Numeric, Placeholder = Tr("Очки", "Points") };
        var button = new Button { Text = Tr("Передать →", "Transfer →") };
        button.Clicked += (_, _) =>
        {
            if (picker.SelectedIndex < 0 || !int.TryParse(amount.Text, out var value))
            {
                SetStatus(Tr("Введите получателя и целое количество очков", "Choose a recipient and enter whole points"), true);
                return;
            }
            var result = session.Transfer(playerIndex, targets[picker.SelectedIndex].index, value);
            SetStatus(result.Message, !result.Success);
            BuildGame();
        };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(100), new(GridLength.Auto) }, ColumnSpacing = 6 };
        grid.Add(picker); grid.Add(amount, 1); grid.Add(button, 2);
        return grid;
    }

    private Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, FontSize = IsMobile() ? 11 : 12, Padding = 6, HeightRequest = 54 };
        button.Clicked += (_, _) => action();
        return button;
    }

    private void Buy(int playerIndex, Func<ActionResult> purchase)
    {
        activePlayer = playerIndex;
        var result = purchase();
        expandedPlayer = playerIndex;
        BuildGame();
        SetStatus(result.Message, !result.Success);
    }

    private void BuyExact(int playerIndex, Picker picker)
    {
        if (picker.SelectedItem is not string heroName)
        {
            SetStatus(Tr("Выберите героя", "Choose a hero"), true);
            return;
        }
        Buy(playerIndex, () => session.BuyExact(playerIndex, HeroCatalog.All.First(hero => hero.Name == heroName)));
    }

    private void EmergencyReroll(object? sender, EventArgs e)
    {
        var result = session.EmergencyReroll(activePlayer);
        BuildGame();
        SetStatus(result.Message, !result.Success);
    }

    private async void LoadScreenshot(object? sender, EventArgs e)
    {
        var file = await AcquireScreenshotAsync(Tr("Таблица матча", "Scoreboard"));
        if (file is null) return;
        ScreenshotButton.IsEnabled = false;
        SetStatus(Tr("VLM распознаёт скриншот…", "VLM is reading the screenshot…"));
        try
        {
            var result = await ScoreboardOcr.ReadAsync(file, session.Players.Select(player => player.Name).ToArray());
            foreach (var (index, stats) in result)
            {
                if (index < 0 || index >= statValues.Count) continue;
                statValues[index] =
                [stats.Eliminations.ToString(), FormatObjective(stats.ObjectiveSeconds), stats.Damage.ToString(), stats.Healing.ToString(), stats.Deaths.ToString()];
            }
            BuildGame();
            SetStatus(Tr($"VLM: заполнено {result.Count}/{session.Players.Count}, проверьте значения", $"VLM: filled {result.Count}/{session.Players.Count}; verify the values"), result.Count == 0);
        }
        catch (Exception error)
        {
            SetStatus(Tr($"VLM: {error.Message}. Доступен ручной ввод", $"VLM: {error.Message}. Manual input is available"), true);
        }
        finally { ScreenshotButton.IsEnabled = true; }
    }

    private async Task<FileResult?> AcquireScreenshotAsync(string title)
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                SetStatus(Tr("Камера недоступна на этом устройстве", "Camera is unavailable on this device"), true);
                return null;
            }
            return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = title, MaximumWidth = 1280, MaximumHeight = 1280,
                CompressionQuality = 85, RotateImage = true,
            });
        }

        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".bmp"],
        });
        return await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = types });
    }

    private async void FinishRound(object? sender, EventArgs e)
    {
        try
        {
            for (var index = 0; index < session.Players.Count; index++)
            {
                var values = statValues[index];
                session.Players[index].Stats = new RoundStats
                {
                    Eliminations = ScoreboardParser.ParseNumber(values[0]),
                    ObjectiveSeconds = ScoreboardParser.ParseObjective(values[1]),
                    Damage = ScoreboardParser.ParseNumber(values[2]),
                    Healing = ScoreboardParser.ParseNumber(values[3]),
                    Deaths = ScoreboardParser.ParseNumber(values[4]),
                };
            }
        }
        catch (FormatException)
        {
            SetStatus(Tr("Проверьте статистику: нужны неотрицательные числа, ПОГЛ допускает мм:сс", "Check stats: use non-negative numbers; OBJ accepts mm:ss"), true);
            return;
        }
        var gains = session.FinishRound();
        ResetStatValues();
        BuildGame();
        await DisplayAlertAsync(Tr("Раунд завершён", "Round complete"),
            string.Join(Environment.NewLine, session.Players.Select((player, index) => $"{player.Name}: {gains[index]:+0;-0;0}")), Tr("Готово", "Done"));
    }

    private void ResetStatValues()
    {
        statValues.Clear();
        for (var index = 0; index < session.Players.Count; index++) statValues.Add(["0", "0", "0", "0", "0"]);
    }

    private void SetStatus(string text, bool error = false)
    {
        if (statusLabel is null) return;
        statusLabel.Text = text;
        statusLabel.TextColor = error ? Color.FromArgb("#FF6470") : Muted;
    }

    private static Label SmallLabel(string text) => new() { Text = text, TextColor = Muted, FontSize = 12, VerticalTextAlignment = TextAlignment.Center };
    private static Border PickerBorder(Picker picker) => new() { Content = picker, Stroke = Accent, StrokeThickness = 1, Padding = 0 };
    private static Image HeroImage(Hero hero, double size) => new() { Source = hero.ImageName, WidthRequest = size, HeightRequest = size, Aspect = Aspect.AspectFit };
    private static string RoleSymbol(Role role) => role switch { Role.Tank => "◆", Role.Damage => "‼", _ => "+" };
    private string RoleName(Role role) => role switch
    {
        Role.Tank => Tr("Танк", "Tank"), Role.Damage => Tr("Урон", "Damage"), _ => Tr("Поддержка", "Support"),
    };
    private static Color RoleColor(Role role) => role switch
    {
        Role.Tank => Color.FromArgb("#39A8FF"), Role.Damage => Color.FromArgb("#FF5260"), _ => Color.FromArgb("#F3C84B"),
    };
    private static string FormatObjective(int seconds) => seconds >= 60 ? $"{seconds / 60}:{seconds % 60:00}" : seconds.ToString();
}
