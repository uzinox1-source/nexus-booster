using System.IO;
using System.Windows;

namespace NexusBooster.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var consentPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusBooster",
            "disclaimer-accepted.txt");

        if (!File.Exists(consentPath))
        {
            var answer = MessageBox.Show(
                "NEXUS BOOSTER — AS-IS NOTICE\n\n" +
                "This free, open-source utility is provided “AS IS”, without warranty of any kind. " +
                "Windows tweaks can change how your account behaves. Review each warning, keep a backup, " +
                "and apply changes only if you understand them. You are responsible for changes you choose to make.\n\n" +
                "Nexus Booster does not provide overclocking controls, install drivers, or guarantee FPS or ping improvements. " +
                "You can restore Nexus-managed settings in Recovery before removing the app. No background service or scheduled task is installed.\n\n" +
                "Select Yes to accept this notice and continue.",
                "Nexus Booster — First launch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
            {
                Shutdown();
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(consentPath)!);
                File.WriteAllText(consentPath, $"Accepted {DateTimeOffset.UtcNow:O}");
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    $"The acceptance record could not be saved, so Nexus Booster will close.\n\n{error.Message}",
                    "Could not save acceptance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        new MainWindow().Show();
    }
}
