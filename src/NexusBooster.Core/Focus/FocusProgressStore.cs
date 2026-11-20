using System.Text.Json;

namespace NexusBooster.Core.Focus;

public sealed record DailyFocusProgress(
    DateOnly Day,
    int FocusMinutes = 0,
    int FocusBlocks = 0,
    int ReadingMinutes = 0,
    int PushUpReps = 0,
    int WalkingMinutes = 0,
    bool ShortsFree = false);

public sealed record FocusProgress(
    int TotalFocusMinutes,
    int FocusBlocks,
    int IntellectMinutes,
    int PushUpReps,
    int WalkingMinutes,
    int ShortsFreeDays,
    int Rank,
    bool RecoveryRequired,
    DailyFocusProgress Today)
{
    public bool IsQuestComplete =>
        Today.FocusMinutes >= 25 &&
        (Today.PushUpReps >= 20 || Today.ReadingMinutes >= 15);
}

public enum BodyEffort
{
    PushUps,
    Walking
}

public sealed class FocusProgressStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FocusProgressStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<FocusProgress> ReadAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            EvaluateRecovery(state, currentDay);
            return CreateSummary(state, currentDay);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<FocusProgress> RecordFocusBlockAsync(
        int minutes,
        bool isBoss,
        bool isRecovery,
        DateOnly? day = null,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(day, state =>
        {
            var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
            EvaluateRecovery(state, currentDay);

            if (isBoss && (minutes != 90 || currentDay.DayOfWeek != DayOfWeek.Sunday))
            {
                throw new InvalidOperationException("A 90-minute boss session is available only on Sunday.");
            }

            if (!isBoss && minutes != 25)
            {
                throw new ArgumentOutOfRangeException(nameof(minutes), "A standard or recovery session is 25 minutes.");
            }

            if (isRecovery != state.RecoveryRequired)
            {
                throw new InvalidOperationException(
                    state.RecoveryRequired
                        ? "Complete a 25-minute recovery session before starting another block."
                        : "There is no missed-day recovery session to complete.");
            }

            var today = GetDay(state, currentDay);
            SetDay(state, currentDay, today with
            {
                FocusMinutes = checked(today.FocusMinutes + minutes),
                FocusBlocks = checked(today.FocusBlocks + 1)
            });
            state.LastFocusDay = currentDay;
            state.RecoveryRequired = false;
            if (isBoss)
            {
                state.Rank = checked(state.Rank + 1);
            }
        }, cancellationToken);

    public Task<FocusProgress> RecordReadingAsync(
        int minutes,
        DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        ValidateQuantity(minutes, 1, 720, nameof(minutes));
        return UpdateAsync(day, state =>
        {
            var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
            var today = GetDay(state, currentDay);
            SetDay(state, currentDay, today with { ReadingMinutes = checked(today.ReadingMinutes + minutes) });
        }, cancellationToken);
    }

    public Task<FocusProgress> RecordBodyEffortAsync(
        BodyEffort effort,
        int amount,
        DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        ValidateQuantity(amount, 1, 10000, nameof(amount));
        return UpdateAsync(day, state =>
        {
            var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
            var today = GetDay(state, currentDay);
            var updated = effort switch
            {
                BodyEffort.PushUps => today with { PushUpReps = checked(today.PushUpReps + amount) },
                BodyEffort.Walking => today with { WalkingMinutes = checked(today.WalkingMinutes + amount) },
                _ => throw new ArgumentOutOfRangeException(nameof(effort))
            };
            SetDay(state, currentDay, updated);
        }, cancellationToken);
    }

    public Task<FocusProgress> RecordShortsFreeDayAsync(
        DateOnly? day = null,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(day, state =>
        {
            var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
            var today = GetDay(state, currentDay);
            SetDay(state, currentDay, today with { ShortsFree = true });
        }, cancellationToken);

    private async Task<FocusProgress> UpdateAsync(
        DateOnly? day,
        Action<ProgressFile> update,
        CancellationToken cancellationToken)
    {
        var currentDay = day ?? DateOnly.FromDateTime(DateTime.Today);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            EvaluateRecovery(state, currentDay);
            update(state);
            await WriteStateAsync(state, cancellationToken);
            return CreateSummary(state, currentDay);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProgressFile> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new ProgressFile();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return JsonSerializer.Deserialize<ProgressFile>(json)
                ?? throw new InvalidDataException("Focus progress data is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException(
                "Focus progress data could not be read. The existing file was left unchanged.",
                error);
        }
    }

    private async Task WriteStateAsync(ProgressFile state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The focus progress file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void EvaluateRecovery(ProgressFile state, DateOnly currentDay)
    {
        if (state.LastFocusDay is { } lastFocusDay && currentDay.DayNumber - lastFocusDay.DayNumber > 1)
        {
            state.RecoveryRequired = true;
        }
    }

    private static FocusProgress CreateSummary(ProgressFile state, DateOnly currentDay)
    {
        var today = GetDay(state, currentDay);
        return new FocusProgress(
            state.Days.Sum(record => record.FocusMinutes),
            state.Days.Sum(record => record.FocusBlocks),
            state.Days.Sum(record => record.ReadingMinutes),
            state.Days.Sum(record => record.PushUpReps),
            state.Days.Sum(record => record.WalkingMinutes),
            state.Days.Count(record => record.ShortsFree),
            state.Rank,
            state.RecoveryRequired,
            today);
    }

    private static DailyFocusProgress GetDay(ProgressFile state, DateOnly day) =>
        state.Days.FirstOrDefault(record => record.Day == day) ?? new DailyFocusProgress(day);

    private static void SetDay(ProgressFile state, DateOnly day, DailyFocusProgress updated)
    {
        var index = state.Days.FindIndex(record => record.Day == day);
        if (index < 0)
        {
            state.Days.Add(updated);
        }
        else
        {
            state.Days[index] = updated;
        }
    }

    private static void ValidateQuantity(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Enter a value from {minimum} to {maximum}.");
        }
    }

    private sealed class ProgressFile
    {
        public int Rank { get; set; }
        public DateOnly? LastFocusDay { get; set; }
        public bool RecoveryRequired { get; set; }
        public List<DailyFocusProgress> Days { get; set; } = [];
    }
}
