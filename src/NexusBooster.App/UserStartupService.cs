using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using NexusBooster.Core;

namespace NexusBooster.App;

public sealed record UserStartupEntry(string Name, string Command, bool IsDisabled, string TweakId);

public sealed record UserStartupScan(IReadOnlyList<UserStartupEntry> Entries, int UnsupportedCount);

public sealed class UserStartupService(TweakService tweaks)
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

    public async Task<UserStartupScan> ReadAsync(CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, UserStartupEntry>(StringComparer.OrdinalIgnoreCase);
        var unsupportedCount = 0;
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
        {
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    var kind = key.GetValueKind(name);
                    var settingKind = kind switch
                    {
                        RegistryValueKind.String => SettingKind.String,
                        RegistryValueKind.ExpandString => SettingKind.ExpandString,
                        _ => (SettingKind?)null
                    };
                    if (settingKind is null)
                    {
                        unsupportedCount++;
                        continue;
                    }

                    var command = Convert.ToString(
                        key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                        System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    var id = TweakId(name);
                    entries[id] = new UserStartupEntry(name, command, tweaks.IsApplied(id), id);
                }
            }
        }

        foreach (var id in tweaks.GetAppliedIds("startup-"))
        {
            if (entries.ContainsKey(id))
            {
                continue;
            }

            var saved = await tweaks.ReadSavedSettingsAsync(id, cancellationToken);
            var original = saved.FirstOrDefault(setting =>
                string.Equals(setting.KeyPath, RunKeyPath, StringComparison.OrdinalIgnoreCase));
            if (original?.OriginalValue is null)
            {
                continue;
            }

            entries[id] = new UserStartupEntry(original.ValueName, original.OriginalValue.Data, true, id);
        }

        return new UserStartupScan(
            entries.Values.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            unsupportedCount);
    }

    public Task DisableAsync(UserStartupEntry entry, CancellationToken cancellationToken = default)
    {
        var tweak = new TweakDefinition(
            entry.TweakId,
            $"Disable startup item: {entry.Name}",
            "Removes this current-user startup entry and saves its exact original value for rollback.",
            [new TweakValue(RunKeyPath, entry.Name, null)],
            "The item will not launch automatically the next time you sign in.");
        return tweaks.ApplyAsync(tweak, cancellationToken);
    }

    public Task RestoreAsync(UserStartupEntry entry, CancellationToken cancellationToken = default) =>
        tweaks.RestoreAsync(entry.TweakId, cancellationToken);

    private static string TweakId(string valueName)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(valueName));
        return "startup-" + Convert.ToHexString(digest);
    }
}
