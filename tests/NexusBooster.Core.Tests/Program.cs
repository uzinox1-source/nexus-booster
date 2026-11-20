using System.Net;
using System.Text;
using System.Text.Json;
using NexusBooster.Core;
using NexusBooster.Core.Focus;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Apply and restore original values", ApplyAndRestoreOriginalValues),
    ("Restore removes values that did not exist", RestoreDeletesNewValues),
    ("Apply removes a value and rollback restores its type", RemoveAndRestoreValue),
    ("Failed apply rolls back every value", FailedApplyRollsBack),
    ("Reject duplicate apply", RejectDuplicateApply),
    ("Mouse profiles validate and replace by name", SaveMouseProfiles),
    ("Catalog categories include warnings for moderate-risk changes", CatalogCategoriesAndWarnings),
    ("Advanced tuner guidance is visible and never applies changes", TunerGuidanceIsAdvisory),
    ("Restore all recovers valid settings and keeps failed backups", RestoreAllBackups),
    ("Clear mouse profiles preserves a recovery copy", BackupAndClearMouseProfiles),
    ("Restore mouse profiles from backup keeps the backup", RestoreMouseProfiles),
    ("Declining first-launch notice does not save consent", DeclineFirstLaunchConsent),
    ("Accepting first-launch notice persists across launches", AcceptFirstLaunchConsent),
    ("Focus quest requires both a passed block and an IRL check-in", FocusQuestRequirements),
    ("Missed focus day starts a recovery block without erasing progress", FocusRecoveryPreservesProgress),
    ("Critique parser enforces PASS or FAIL and five findings maximum", CritiqueParserLimits),
    ("Critique request stays on the fixed local Ollama endpoint", CritiqueStaysLocal)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failures.Add($"{test.Name}: {error}");
        Console.WriteLine($"FAIL {test.Name}: {error.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;

static async Task ApplyAndRestoreOriginalValues()
{
    using var temp = new TemporaryDirectory();
    var store = new MemorySettingsStore();
    store.Write("Software\\Test", "Value", new SettingValue(SettingKind.String, "before"));
    var tweak = new TweakDefinition("test", "Test tweak", "description",
        [new TweakValue("Software\\Test", "Value", new SettingValue(SettingKind.String, "after"))], "restart");
    var service = new TweakService(store, temp.Path);

    await service.ApplyAsync(tweak);
    Assert.Equal("after", store.Read("Software\\Test", "Value")?.Data);
    Assert.True(service.IsApplied("test"));

    await service.RestoreAsync("test");
    Assert.Equal("before", store.Read("Software\\Test", "Value")?.Data);
    Assert.False(service.IsApplied("test"));
}

static async Task RestoreDeletesNewValues()
{
    using var temp = new TemporaryDirectory();
    var store = new MemorySettingsStore();
    var tweak = new TweakDefinition("new", "New setting", "description",
        [new TweakValue("Software\\Test", "New", new SettingValue(SettingKind.DWord, "1"))], "restart");
    var service = new TweakService(store, temp.Path);

    await service.ApplyAsync(tweak);
    await service.RestoreAsync("new");
    Assert.Null(store.Read("Software\\Test", "New"));
}

static async Task RemoveAndRestoreValue()
{
    using var temp = new TemporaryDirectory();
    var store = new MemorySettingsStore();
    store.Write("Software\\Test", "Startup", new SettingValue(SettingKind.ExpandString, "%LOCALAPPDATA%\\app.exe"));
    var tweak = new TweakDefinition("remove", "Remove value", "description",
        [new TweakValue("Software\\Test", "Startup", null)], "restart");
    var service = new TweakService(store, temp.Path);

    await service.ApplyAsync(tweak);
    Assert.Null(store.Read("Software\\Test", "Startup"));
    await service.RestoreAsync("remove");
    Assert.Equal(new SettingValue(SettingKind.ExpandString, "%LOCALAPPDATA%\\app.exe"), store.Read("Software\\Test", "Startup"));
}

static async Task FailedApplyRollsBack()
{
    using var temp = new TemporaryDirectory();
    var store = new FailOnceSettingsStore("Second");
    store.WriteInitial("First", new SettingValue(SettingKind.String, "before-first"));
    store.WriteInitial("Second", new SettingValue(SettingKind.String, "before-second"));
    var tweak = new TweakDefinition("rollback", "Rollback", "description",
    [
        new TweakValue("Software\\Test", "First", new SettingValue(SettingKind.String, "after-first")),
        new TweakValue("Software\\Test", "Second", new SettingValue(SettingKind.String, "after-second"))
    ], "restart");
    var service = new TweakService(store, temp.Path);

    await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(tweak));
    Assert.Equal("before-first", store.Read("Software\\Test", "First")?.Data);
    Assert.Equal("before-second", store.Read("Software\\Test", "Second")?.Data);
    Assert.False(service.IsApplied("rollback"));
}

static async Task RejectDuplicateApply()
{
    using var temp = new TemporaryDirectory();
    var tweak = new TweakDefinition("duplicate", "Duplicate", "description",
        [new TweakValue("Software\\Test", "Value", new SettingValue(SettingKind.DWord, "1"))], "restart");
    var service = new TweakService(new MemorySettingsStore(), temp.Path);

    await service.ApplyAsync(tweak);
    await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(tweak));
}

static async Task SaveMouseProfiles()
{
    using var temp = new TemporaryDirectory();
    var service = new MouseProfileService(Path.Combine(temp.Path, "profiles.json"));
    await service.SaveAsync(new MouseDpiProfile("Gaming", 800));
    await service.SaveAsync(new MouseDpiProfile("gaming", 1600));
    var profiles = await service.ReadAsync();
    Assert.Equal(1, profiles.Count);
    Assert.Equal(1600, profiles[0].Dpi);
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveAsync(new MouseDpiProfile("Bad", 0)));
}

static Task CatalogCategoriesAndWarnings()
{
    Assert.True(TweakCatalog.All.Count >= 7);
    Assert.True(TweakCatalog.All.All(tweak => !string.IsNullOrWhiteSpace(tweak.Category)));
    Assert.True(TweakCatalog.All
        .Where(tweak => tweak.Risk != "Low")
        .All(tweak => !string.IsNullOrWhiteSpace(tweak.Warning)));
    Assert.True(TweakCatalog.All.Any(tweak => tweak.Category == TweakCategories.Fps));
    Assert.True(TweakCatalog.All.Any(tweak => tweak.Category == TweakCategories.Ui));
    Assert.False(TweakCatalog.All.Any(tweak => tweak.Values.Any(value => value.KeyPath.StartsWith("Software\\Policies\\", StringComparison.Ordinal))));
    return Task.CompletedTask;
}

static Task TunerGuidanceIsAdvisory()
{
    var requiredCategories = new[]
    {
        TweakCategories.Fps,
        TweakCategories.Ping,
        TweakCategories.Power,
        TweakCategories.Privacy,
        TweakCategories.Maintenance
    };

    foreach (var category in requiredCategories)
    {
        var guidance = TweakGuidanceCatalog.All.Where(item => item.Category == category).ToArray();
        Assert.True(guidance.Length > 0);
        Assert.True(guidance.All(item =>
            !string.IsNullOrWhiteSpace(item.Title) &&
            !string.IsNullOrWhiteSpace(item.Description) &&
            !string.IsNullOrWhiteSpace(item.Caution)));
    }

    return Task.CompletedTask;
}

static async Task RestoreAllBackups()
{
    using var temp = new TemporaryDirectory();
    var store = new MemorySettingsStore();
    store.Write("Software\\Test", "Value", new SettingValue(SettingKind.String, "before"));
    var service = new TweakService(store, temp.Path);
    await service.ApplyAsync(new TweakDefinition(
        "first",
        "First",
        "description",
        [new TweakValue("Software\\Test", "Value", new SettingValue(SettingKind.String, "after"))],
        "restart"));

    Directory.CreateDirectory(temp.Path);
    await File.WriteAllTextAsync(Path.Combine(temp.Path, "broken.json"), "{not json");
    var result = await service.RestoreAllAsync();

    Assert.Equal(1, result.RestoredIds.Count);
    Assert.Equal("first", result.RestoredIds[0]);
    Assert.Equal("before", store.Read("Software\\Test", "Value")?.Data);
    Assert.Equal(1, result.Failures.Count);
    Assert.Equal("broken", result.Failures[0].BackupId);
    Assert.True(File.Exists(Path.Combine(temp.Path, "broken.json")));
}

static async Task BackupAndClearMouseProfiles()
{
    using var temp = new TemporaryDirectory();
    var profilePath = Path.Combine(temp.Path, "mouse-profiles.json");
    var service = new MouseProfileService(profilePath);
    await service.SaveAsync(new MouseDpiProfile("Gaming", 800));

    var backup = await service.BackupAndClearAsync(Path.Combine(temp.Path, "recovery"));

    Assert.True(backup is not null);
    Assert.True(File.Exists(backup!));
    Assert.Equal(0, (await service.ReadAsync()).Count);
    var recovered = await File.ReadAllTextAsync(backup!);
    Assert.True(recovered.Contains("Gaming", StringComparison.Ordinal));
}

static async Task RestoreMouseProfiles()
{
    using var temp = new TemporaryDirectory();
    var file = Path.Combine(temp.Path, "mouse-profiles.json");
    var service = new MouseProfileService(file);
    await service.SaveAsync(new MouseDpiProfile("Gaming", 800));
    var backup = await service.BackupAndClearAsync(Path.Combine(temp.Path, "recovery"))
        ?? throw new InvalidOperationException("Expected a profile backup.");

    await service.RestoreBackupAsync(backup);

    Assert.Equal(800, (await service.ReadAsync()).Single().Dpi);
    Assert.True(File.Exists(backup));
}

static Task DeclineFirstLaunchConsent()
{
    using var temp = new TemporaryDirectory();
    var consent = new FirstLaunchConsent(Path.Combine(temp.Path, "consent.txt"));
    var prompted = false;

    var accepted = consent.EnsureAccepted(() =>
    {
        prompted = true;
        return false;
    });

    Assert.True(prompted);
    Assert.False(accepted);
    Assert.False(File.Exists(Path.Combine(temp.Path, "consent.txt")));
    return Task.CompletedTask;
}

static Task AcceptFirstLaunchConsent()
{
    using var temp = new TemporaryDirectory();
    var path = Path.Combine(temp.Path, "consent.txt");
    var consent = new FirstLaunchConsent(path);

    Assert.True(consent.EnsureAccepted(() => true));
    Assert.True(File.Exists(path));
    Assert.True(new FirstLaunchConsent(path).EnsureAccepted(
        () => throw new InvalidOperationException("An accepted notice must not be shown again.")));
    return Task.CompletedTask;
}

static async Task FocusQuestRequirements()
{
    using var temp = new TemporaryDirectory();
    var store = new FocusProgressStore(Path.Combine(temp.Path, "focus.json"));
    var today = new DateOnly(2026, 11, 20);

    await store.RecordReadingAsync(15, today);
    await store.RecordBodyEffortAsync(BodyEffort.PushUps, 20, today);
    Assert.False((await store.ReadAsync(today)).IsQuestComplete);

    var progress = await store.RecordFocusBlockAsync(25, isBoss: false, isRecovery: false, today);
    Assert.True(progress.IsQuestComplete);
    Assert.Equal(25, progress.TotalFocusMinutes);
    Assert.Equal(15, progress.IntellectMinutes);
    Assert.Equal(20, progress.PushUpReps);
}

static async Task FocusRecoveryPreservesProgress()
{
    using var temp = new TemporaryDirectory();
    var store = new FocusProgressStore(Path.Combine(temp.Path, "focus.json"));
    var monday = new DateOnly(2026, 11, 16);
    await store.RecordFocusBlockAsync(25, isBoss: false, isRecovery: false, monday);

    var missedDay = await store.ReadAsync(monday.AddDays(2));
    Assert.True(missedDay.RecoveryRequired);
    Assert.Equal(25, missedDay.TotalFocusMinutes);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        store.RecordFocusBlockAsync(25, isBoss: false, isRecovery: false, monday.AddDays(2)));

    var recovered = await store.RecordFocusBlockAsync(25, isBoss: false, isRecovery: true, monday.AddDays(2));
    Assert.False(recovered.RecoveryRequired);
    Assert.Equal(50, recovered.TotalFocusMinutes);
    Assert.Equal(2, recovered.FocusBlocks);
}

static Task CritiqueParserLimits()
{
    var response = """
        {"verdict":"PASS","bullets":["one","two","three","four","five","six"]}
        """;
    var critique = LocalCritiqueService.ParseFeedback(response);
    Assert.True(critique.Passed);
    Assert.Equal(5, critique.Bullets.Count);

    var failure = LocalCritiqueService.ParseFeedback("""{"verdict":"FAIL","bullets":["Fix the missing case."]}""");
    Assert.False(failure.Passed);
    var bloat = LocalCritiqueService.ParseFeedback(
        """{"verdict":"FAIL","bullets":["Cut: 35%"]}""",
        requireCutPercent: true);
    Assert.False(bloat.Passed);
    Assert.Throws<InvalidDataException>(() =>
        LocalCritiqueService.ParseFeedback("""{"verdict":"FAIL","bullets":["Cut 35 percent"]}""", requireCutPercent: true));
    Assert.Throws<InvalidDataException>(() =>
        LocalCritiqueService.ParseFeedback("""{"verdict":"MAYBE","bullets":["unclear"]}"""));
    return Task.CompletedTask;
}

static async Task CritiqueStaysLocal()
{
    var handler = new CaptureCritiqueHandler();
    using var service = new LocalCritiqueService(handler);
    var result = await service.CritiqueAsync(CritiqueMode.CodeAuditor, "Fix the parser", "Check empty input.");

    Assert.True(result.Passed);
    Assert.Equal("127.0.0.1", handler.RequestUri?.Host);
    Assert.Equal("/api/generate", handler.RequestUri?.AbsolutePath);
    using var payload = JsonDocument.Parse(handler.RequestBody!);
    Assert.Equal(LocalCritiqueService.DefaultModel, payload.RootElement.GetProperty("model").GetString());
    Assert.True(payload.RootElement.GetProperty("prompt").GetString()!.Contains("Fix the parser", StringComparison.Ordinal));
}

sealed class MemorySettingsStore : IUserSettingsStore
{
    private readonly Dictionary<(string Key, string Name), SettingValue> _values = [];

    public SettingValue? Read(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) ? value : null;

    public void Write(string keyPath, string valueName, SettingValue value) =>
        _values[(keyPath, valueName)] = value;

    public void Delete(string keyPath, string valueName) =>
        _values.Remove((keyPath, valueName));
}

sealed class CaptureCritiqueHandler : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        const string critique = """{"verdict":"PASS","bullets":["No blocking issue."]}""";
        var response = JsonSerializer.Serialize(new { response = critique });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}

sealed class FailOnceSettingsStore(string valueName) : IUserSettingsStore
{
    private readonly MemorySettingsStore _inner = new();
    private bool _shouldFail = true;

    public SettingValue? Read(string keyPath, string name) => _inner.Read(keyPath, name);

    public void Write(string keyPath, string name, SettingValue value)
    {
        if (_shouldFail && name == valueName)
        {
            _shouldFail = false;
            throw new IOException("Simulated write failure.");
        }

        _inner.Write(keyPath, name, value);
    }

    public void WriteInitial(string name, SettingValue value) => _inner.Write("Software\\Test", name, value);

    public void Delete(string keyPath, string name) => _inner.Delete(keyPath, name);
}

sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NexusBooster.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

static class Assert
{
    public static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    public static void False(bool value)
    {
        if (value) throw new InvalidOperationException("Expected false.");
    }

    public static void Null(object? value)
    {
        if (value is not null) throw new InvalidOperationException($"Expected null, got {value}.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    public static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    public static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
