using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Threading;
using NexusBooster.Core;

namespace NexusBooster.App;

public partial class App : Application
{
    private string? _startupLogPath;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var stage = "checking first-launch consent";
        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexusBooster");
            _startupLogPath = Path.Combine(dataDirectory, "startup-error.log");

            var consent = new FirstLaunchConsent(Path.Combine(dataDirectory, "disclaimer-accepted.txt"));
            if (!consent.EnsureAccepted(ShowConsentNotice))
            {
                Shutdown();
                return;
            }

            stage = "opening the main window";
            new MainWindow().Show();
        }
        catch (Exception error)
        {
            ShowFailure($"Nexus Safe Tweaks could not start while {stage}.", stage, error);
            Shutdown(-1);
        }
    }

    private static bool ShowConsentNotice() =>
        MessageBox.Show(
            "NEXUS SAFE TWEAKS — AS-IS NOTICE\n\n" +
            "This free, open-source utility is provided “AS IS”, without warranty of any kind. " +
            "Windows tweaks can change how your account behaves. Review each warning, keep a backup, " +
            "and apply changes only if you understand them. You are responsible for changes you choose to make.\n\n" +
            "Nexus Safe Tweaks does not provide overclocking controls, install drivers, or guarantee FPS or ping improvements. " +
            "You can restore Nexus-managed settings in Recovery before removing the app. No background service or scheduled task is installed.\n\n" +
            "Select Yes to accept this notice and continue.",
            "Nexus Safe Tweaks — First launch",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFailure(
            "Nexus Safe Tweaks encountered an unexpected error and will close.",
            "unhandled WPF dispatcher exception",
            e.Exception);
        e.Handled = true;
        Shutdown(-1);
    }

    private void ShowFailure(string message, string stage, Exception error)
    {
        var logSaved = TryWriteStartupLog(stage, error);
        var diagnostic = logSaved
            ? "A local diagnostic entry was saved to %LOCALAPPDATA%\\NexusBooster\\startup-error.log."
            : "The local diagnostic log could not be saved.";
        MessageBox.Show(
            $"{message}\n\n{diagnostic}",
            "Nexus Safe Tweaks error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private bool TryWriteStartupLog(string stage, Exception error)
    {
        if (_startupLogPath is null)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_startupLogPath)!);
            var entry = $"{DateTimeOffset.UtcNow:O}\t{stage}\t{error.GetType().FullName}\t0x{error.HResult:X8}{Environment.NewLine}";
            File.AppendAllText(_startupLogPath, entry);
            return true;
        }
        catch (Exception logError) when (
            logError is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
