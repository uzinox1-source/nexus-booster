namespace NexusBooster.Core;

public enum SettingKind
{
    String,
    ExpandString,
    DWord
}

public sealed record SettingValue(SettingKind Kind, string Data);

public sealed record TweakValue(
    string KeyPath,
    string ValueName,
    SettingValue? Value);

public sealed record TweakDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<TweakValue> Values,
    string RestartNote);

public sealed record SavedSetting(
    string KeyPath,
    string ValueName,
    SettingValue? OriginalValue);

public interface IUserSettingsStore
{
    SettingValue? Read(string keyPath, string valueName);
    void Write(string keyPath, string valueName, SettingValue value);
    void Delete(string keyPath, string valueName);
}
