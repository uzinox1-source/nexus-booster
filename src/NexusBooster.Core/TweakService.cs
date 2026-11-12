using System.Text.Json;

namespace NexusBooster.Core;

public sealed class TweakService(IUserSettingsStore settings, string backupDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public bool IsApplied(string tweakId) => File.Exists(BackupPath(tweakId));

    public IReadOnlyList<string> GetAppliedIds(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The backup prefix is invalid.", nameof(prefix));
        }

        return Directory.Exists(backupDirectory)
            ? Directory.EnumerateFiles(backupDirectory, prefix + "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => id is not null)
                .Select(id => id!)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    public IReadOnlyList<string> GetAllAppliedIds() =>
        Directory.Exists(backupDirectory)
            ? Directory.EnumerateFiles(backupDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => id is not null)
                .Select(id => id!)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : [];

    public async Task<RestoreAllResult> RestoreAllAsync(CancellationToken cancellationToken = default)
    {
        var restored = new List<string>();
        var failures = new List<RestoreFailure>();
        foreach (var id in GetAllAppliedIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await RestoreAsync(id, cancellationToken);
                restored.Add(id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                failures.Add(new RestoreFailure(id, error.Message));
            }
        }

        return new RestoreAllResult(restored, failures);
    }

    public async Task<IReadOnlyList<SavedSetting>> ReadSavedSettingsAsync(
        string tweakId,
        CancellationToken cancellationToken = default)
    {
        var backupPath = BackupPath(tweakId);
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException($"No saved backup exists for '{tweakId}'.");
        }

        await using var stream = File.OpenRead(backupPath);
        return await JsonSerializer.DeserializeAsync<SavedSetting[]>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"The settings backup for '{tweakId}' is empty.");
    }

    public async Task ApplyAsync(TweakDefinition tweak, CancellationToken cancellationToken = default)
    {
        Validate(tweak);
        var backupPath = BackupPath(tweak.Id);
        if (File.Exists(backupPath))
        {
            throw new InvalidOperationException($"'{tweak.Name}' is already applied.");
        }

        var saved = tweak.Values
            .Select(value => new SavedSetting(value.KeyPath, value.ValueName, settings.Read(value.KeyPath, value.ValueName)))
            .ToArray();
        await WriteBackupAsync(backupPath, saved, cancellationToken);

        try
        {
            foreach (var value in tweak.Values)
            {
                if (value.Value is null)
                {
                    settings.Delete(value.KeyPath, value.ValueName);
                }
                else
                {
                    settings.Write(value.KeyPath, value.ValueName, value.Value);
                }
            }
        }
        catch (Exception applyError)
        {
            try
            {
                RestoreValues(saved);
                File.Delete(backupPath);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException(
                    $"Applying '{tweak.Name}' failed and automatic rollback was incomplete. Keep the backup at '{backupPath}'.",
                    applyError,
                    rollbackError);
            }

            throw new InvalidOperationException($"Applying '{tweak.Name}' failed; the original settings were restored.", applyError);
        }
    }

    public async Task RestoreAsync(string tweakId, CancellationToken cancellationToken = default)
    {
        var backupPath = BackupPath(tweakId);
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException($"No saved backup exists for '{tweakId}'.");
        }

        var saved = await ReadSavedSettingsAsync(tweakId, cancellationToken);
        RestoreValues(saved);
        File.Delete(backupPath);
    }

    private void RestoreValues(IEnumerable<SavedSetting> saved)
    {
        List<Exception>? errors = null;
        foreach (var setting in saved)
        {
            try
            {
                if (setting.OriginalValue is null)
                {
                    settings.Delete(setting.KeyPath, setting.ValueName);
                }
                else
                {
                    settings.Write(setting.KeyPath, setting.ValueName, setting.OriginalValue);
                }
            }
            catch (Exception error)
            {
                (errors ??= []).Add(error);
            }
        }

        if (errors is not null)
        {
            throw new AggregateException("One or more saved settings could not be restored.", errors);
        }
    }

    private async Task WriteBackupAsync(
        string destination,
        IReadOnlyCollection<SavedSetting> saved,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(backupDirectory);
        var temporary = destination + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, saved, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporary, destination);
    }

    private string BackupPath(string tweakId)
    {
        if (string.IsNullOrWhiteSpace(tweakId) || tweakId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The tweak identifier is invalid.", nameof(tweakId));
        }

        return Path.Combine(backupDirectory, tweakId + ".json");
    }

    private static void Validate(TweakDefinition tweak)
    {
        if (tweak.Values.Count == 0)
        {
            throw new ArgumentException("A tweak must contain at least one setting.", nameof(tweak));
        }

        if (tweak.Values.Any(value => string.IsNullOrWhiteSpace(value.KeyPath) ||
                                      string.IsNullOrWhiteSpace(value.ValueName) ||
                                      value.KeyPath.StartsWith('\\') ||
                                      value.KeyPath.Contains("..", StringComparison.Ordinal)))
        {
            throw new ArgumentException("A tweak contains an invalid user settings path.", nameof(tweak));
        }
    }
}
