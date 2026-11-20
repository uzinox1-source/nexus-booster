namespace NexusBooster.Core;

public sealed record TweakGuidance(
    string Category,
    string Title,
    string Description,
    string Caution);

public static class TweakGuidanceCatalog
{
    public static IReadOnlyList<TweakGuidance> All { get; } =
    [
        new(
            TweakCategories.Fps,
            "Advanced GPU and scheduler registry edits",
            "HAGS, TDR, and vendor-specific registry changes can affect stability and vary by Windows build and hardware.",
            "There is no universal performance setting. Nexus does not apply these changes; verify exact-device guidance and keep a recovery path before making them manually."),
        new(
            TweakCategories.Ping,
            "Measure the route before changing network settings",
            "Latency depends on the ISP route, game server, congestion, and Wi-Fi quality. Compare the same server and time of day, test Ethernet versus Wi-Fi, and check for packet loss.",
            "Registry “ping fixes” and DNS changes cannot shorten a route to a game server and may make name resolution or connectivity worse."),
        new(
            TweakCategories.Power,
            "USB, PCIe, and power-plan changes",
            "Power-saving options trade energy use and heat for idle power savings. Effects vary by device and can affect battery life, sleep, or wake behavior.",
            "Avoid blanket changes to USB selective suspend, PCIe Link State Power Management, or hardware power policies. Review Windows or device-maker settings manually."),
        new(
            TweakCategories.Privacy,
            "Security, update, and hosts-file changes",
            "Security and update services protect the system and keep Windows supported. Hosts-file blocks can disrupt updates, sign-in, or vendor support.",
            "Keep Defender, Firewall, UAC, and Windows Update enabled. Nexus does not modify the hosts file, security policies, or system services."),
        new(
            TweakCategories.Maintenance,
            "Cleanup and Windows servicing",
            "Review temporary files in Windows Storage settings before removing them. Cache and component-store cleanup can remove useful data or rollback files.",
            "Nexus does not purge broad caches, disable scheduled tasks, or run DISM ResetBase. Use servicing commands only to address a diagnosed problem.")
    ];
}
