namespace NexusBooster.Core;

public enum SettingKind
{
    String,
    ExpandString,
    DWord
}

public static class TweakCategories
{
    public const string Fps = "FPS & Gaming";
    public const string Ui = "UI & Responsiveness";
    public const string Privacy = "Privacy & Security";
    public const string Ping = "Ping & Network";
    public const string Power = "Power, USB & Hardware";
    public const string Maintenance = "Maintenance";
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
    string RestartNote,
    string Category = "General",
    string Risk = "Low",
    string? Warning = null);

public sealed record SavedSetting(
    string KeyPath,
    string ValueName,
    SettingValue? OriginalValue);

public sealed record RestoreFailure(string BackupId, string Message);

public sealed record RestoreAllResult(
    IReadOnlyList<string> RestoredIds,
    IReadOnlyList<RestoreFailure> Failures);

public interface IUserSettingsStore
{
    SettingValue? Read(string keyPath, string valueName);
    void Write(string keyPath, string valueName, SettingValue value);
    void Delete(string keyPath, string valueName);
}
