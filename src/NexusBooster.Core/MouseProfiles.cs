using System.Text.Json;

namespace NexusBooster.Core;

public sealed record MouseDpiProfile(string Name, int Dpi);

public sealed class MouseProfileService(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<MouseDpiProfile>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<MouseDpiProfile>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    public async Task SaveAsync(MouseDpiProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        var profiles = (await ReadAsync(cancellationToken))
            .Where(item => !string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
            .Append(profile)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = filePath + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporary, filePath, true);
    }

    public async Task<string?> BackupAndClearAsync(
        string recoveryDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        Directory.CreateDirectory(recoveryDirectory);
        var backupPath = Path.Combine(
            recoveryDirectory,
            $"mouse-profiles-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.json");
        await using (var source = File.OpenRead(filePath))
        await using (var destination = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }

        File.Delete(filePath);
        return backupPath;
    }

    public async Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        await using var source = File.OpenRead(backupPath);
        var profiles = await JsonSerializer.DeserializeAsync<List<MouseDpiProfile>>(source, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The selected mouse profile backup is empty.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            Validate(profile);
            if (!names.Add(profile.Name))
            {
                throw new InvalidDataException($"The backup contains duplicate profile names: '{profile.Name}'.");
            }
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = filePath + ".tmp";
        await using (var destination = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(destination, profiles, JsonOptions, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }

        File.Move(temporary, filePath, true);
    }

    private static void Validate(MouseDpiProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 32 ||
            profile.Name.Any(char.IsControl))
        {
            throw new ArgumentException("Profile names must be 1-32 printable characters.", nameof(profile));
        }

        if (profile.Dpi is < 100 or > 25600)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "DPI must be between 100 and 25,600.");
        }
    }
}
