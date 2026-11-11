using System.IO;
using Microsoft.Win32;
using NexusBooster.Core;

namespace NexusBooster.App;

public sealed class RegistryUserSettingsStore : IUserSettingsStore
{
    public SettingValue? Read(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        if (key is null || key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not { } value)
        {
            return null;
        }

        return key.GetValueKind(valueName) switch
        {
            RegistryValueKind.DWord => new SettingValue(SettingKind.DWord, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            RegistryValueKind.String => new SettingValue(SettingKind.String, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
            RegistryValueKind.ExpandString => new SettingValue(SettingKind.ExpandString, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
            _ => throw new NotSupportedException($"The existing registry value '{valueName}' has an unsupported type and will not be changed.")
        };
    }

    public void Write(string keyPath, string valueName, SettingValue value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
            ?? throw new IOException($"Could not open the current-user registry key '{keyPath}'.");
        switch (value.Kind)
        {
            case SettingKind.DWord:
                key.SetValue(valueName, int.Parse(value.Data, System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                break;
            case SettingKind.String:
                key.SetValue(valueName, value.Data, RegistryValueKind.String);
                break;
            case SettingKind.ExpandString:
                key.SetValue(valueName, value.Data, RegistryValueKind.ExpandString);
                break;
            default:
                throw new NotSupportedException($"Registry setting kind '{value.Kind}' is not supported.");
        }
    }

    public void Delete(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
