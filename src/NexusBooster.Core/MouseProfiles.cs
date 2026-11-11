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
