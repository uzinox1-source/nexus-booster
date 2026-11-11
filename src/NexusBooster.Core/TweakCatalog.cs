namespace NexusBooster.Core;

public static class TweakCatalog
{
    public static IReadOnlyList<TweakDefinition> All { get; } =
    [
        new(
            "game-mode",
            "Enable Windows Game Mode",
            "Lets Windows prioritize games when they are running.",
            [Dword("Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 1)],
            "Takes effect the next time a game starts."),
        new(
            "background-capture",
            "Disable background game capture",
            "Turns off background recording for this Windows account. Manual captures may also be affected.",
            [
                Dword("System\\GameConfigStore", "GameDVR_Enabled", 0),
                Dword("Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR", "AppCaptureEnabled", 0)
            ],
            "Restart games that are already open."),
        new(
            "transparency",
            "Reduce transparency effects",
            "Disables Windows transparency for this account to simplify visual effects.",
            [Dword("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", 0)],
            "Windows may refresh the effect after sign-in."),
        new(
            "menu-delay",
            "Speed up menu response",
            "Sets the account menu delay to 100 ms (the common Windows default is 400 ms).",
            [Text("Control Panel\\Desktop", "MenuShowDelay", "100")],
            "Sign out and back in if Windows does not refresh the setting."),
        new(
            "window-animations",
            "Turn off window minimize animations",
            "Disables the minimize/maximize animation for this Windows account.",
            [Text("Control Panel\\Desktop\\WindowMetrics", "MinAnimate", "0")],
            "Sign out and back in if Windows does not refresh the setting."),
        new(
            "mouse-acceleration",
            "Disable mouse acceleration",
            "Turns off Enhance pointer precision for this account. This is separate from mouse DPI.",
            [
                Text("Control Panel\\Mouse", "MouseSpeed", "0"),
                Text("Control Panel\\Mouse", "MouseThreshold1", "0"),
                Text("Control Panel\\Mouse", "MouseThreshold2", "0")
            ],
            "Sign out and back in if Windows does not refresh the setting."),
        new(
            "startup-delay",
            "Reduce Explorer startup delay",
            "Sets the current-user Explorer startup delay to zero milliseconds.",
            [Dword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize", "StartupDelayInMSec", 0)],
            "Applies the next time you sign in.")
    ];

    private static TweakValue Dword(string path, string name, int value) =>
        new(path, name, new SettingValue(SettingKind.DWord, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static TweakValue Text(string path, string name, string value) =>
        new(path, name, new SettingValue(SettingKind.String, value));
}
