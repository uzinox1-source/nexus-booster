using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using NexusBooster.Core;

namespace NexusBooster.App;

public partial class MainWindow : Window
{
    private readonly TweakService _tweaks;
    private readonly MouseProfileService _profiles;
    private readonly UserStartupService _startup;

    public MainWindow()
    {
        InitializeComponent();
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusBooster");
        _tweaks = new TweakService(new RegistryUserSettingsStore(), Path.Combine(dataDirectory, "backups"));
        _profiles = new MouseProfileService(Path.Combine(dataDirectory, "mouse-profiles.json"));
        _startup = new UserStartupService(_tweaks);
        Loaded += async (_, _) =>
        {
            AnimateEntrance();
            await RenderTweaksAsync();
            await UpdateProfileCountAsync();
        };
    }

    private void AnimateEntrance()
    {
        RootShell.Opacity = 0;
        RootShell.RenderTransform = new TranslateTransform(0, 10);
        var duration = TimeSpan.FromMilliseconds(520);
        RootShell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        RootShell.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private Task RenderTweaksAsync()
    {
        TweakList.Children.Clear();
        var applied = 0;
        foreach (var tweak in TweakCatalog.All)
        {
            var isApplied = _tweaks.IsApplied(tweak.Id);
            if (isApplied)
            {
                applied++;
            }

            var button = new Button
            {
                Content = isApplied ? "Undo" : "Apply",
                Background = isApplied ? new SolidColorBrush(Color.FromRgb(52, 73, 70)) : new SolidColorBrush(Color.FromRgb(41, 47, 62)),
                Padding = new Thickness(12, 7, 12, 7),
                MinWidth = 66,
                Tag = tweak
            };
            button.Click += TweakAction_Click;

            var item = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            text.Children.Add(new TextBlock
            {
                Text = tweak.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                Foreground = (Brush)FindResource("TextPrimary")
            });
            text.Children.Add(new TextBlock
            {
                Text = tweak.Description,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextMuted"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(button, 1);
            item.Children.Add(text);
            item.Children.Add(button);
            TweakList.Children.Add(item);
        }

        AppliedCount.Text = applied.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    }

    private async void TweakAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TweakDefinition tweak } button)
        {
            return;
        }

        var isApplied = _tweaks.IsApplied(tweak.Id);
        var message = isApplied
            ? $"Restore the saved settings for “{tweak.Name}”?\n\n{tweak.RestartNote}"
            : $"Apply “{tweak.Name}” to this Windows account?\n\n{tweak.Description}\n\nA backup will be saved first. You can undo it here later.\n{tweak.RestartNote}";
        if (MessageBox.Show(message, isApplied ? "Restore saved settings" : "Review change",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            if (isApplied)
            {
                await _tweaks.RestoreAsync(tweak.Id);
                StatusText.Text = $"Restored “{tweak.Name}” from its saved backup.";
            }
            else
            {
                await _tweaks.ApplyAsync(tweak);
                StatusText.Text = $"Applied “{tweak.Name}”. {tweak.RestartNote}";
            }

            await RenderTweaksAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Could not change setting", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "No success was reported. Review the error before trying again.";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task UpdateProfileCountAsync()
    {
        try
        {
            ProfileCount.Text = (await _profiles.ReadAsync()).Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception error)
        {
            StatusText.Text = $"Could not read mouse profiles: {error.Message}";
        }
    }

    private void ShowDetail(string title, string body, params UIElement[] content)
    {
        PageTitle.Text = title;
        DetailTitle.Text = title;
        DetailBody.Text = body;
        DetailContent.Children.Clear();
        foreach (var element in content)
        {
            DetailContent.Children.Add(element);
        }

        DetailPanel.Visibility = Visibility.Visible;
        DetailPanel.BringIntoView();
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Dashboard";
        DetailPanel.Visibility = Visibility.Collapsed;
    }

    private void Tweaks_Click(object sender, RoutedEventArgs e)
    {
        ShowDetail("Safe tweaks",
            "Each change affects only your current Windows account. Nexus records existing values before writing and keeps an Undo action available until you restore them.");
        TweakList.BringIntoView();
    }

    private async void Debloat_Click(object sender, RoutedEventArgs e)
    {
        var warning = new TextBlock
        {
            Text = "Disable only startup items you recognize. Nexus saves each exact current-user registry value so you can restore it. It will not remove Windows system components or bundled apps.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
            FontSize = 12,
            LineHeight = 19,
            Margin = new Thickness(0, 0, 0, 12)
        };
        ShowDetail("Debloat & startup",
            "Trim background startup load safely, then review optional installed apps in Windows Settings.",
            warning, new TextBlock
            {
                Text = "Current-user startup items",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                Margin = new Thickness(0, 0, 0, 8)
            });
        await RenderStartupEntriesAsync();
        DetailContent.Children.Add(CreateInstalledAppsFooter());
    }

    private StackPanel CreateInstalledAppsFooter()
    {
        var footer = new StackPanel();
        footer.Children.Add(new TextBlock
        {
            Text = "Optional apps can be reviewed in Windows Settings. Some removals cannot be undone if the Store package is unavailable.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 11,
            Margin = new Thickness(0, 18, 0, 8)
        });
        var open = new Button
        {
            Content = "Open Installed apps in Windows Settings  →",
            Background = (Brush)FindResource("AccentGradient"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        open.Click += (_, _) => StartUri("ms-settings:appsfeatures");
        footer.Children.Add(open);
        return footer;
    }

    private async Task RenderStartupEntriesAsync()
    {
        try
        {
            var scan = await _startup.ReadAsync();
            if (scan.Entries.Count == 0)
            {
                DetailContent.Children.Add(new TextBlock
                {
                    Text = "No current-user startup entries were found.",
                    Foreground = (Brush)FindResource("TextMuted"),
                    FontSize = 11
                });
            }

            foreach (var entry in scan.Entries)
            {
                var button = new Button
                {
                    Content = entry.IsDisabled ? "Restore" : "Disable",
                    Background = entry.IsDisabled
                        ? new SolidColorBrush(Color.FromRgb(52, 73, 70))
                        : new SolidColorBrush(Color.FromRgb(41, 47, 62)),
                    Padding = new Thickness(12, 7, 12, 7),
                    MinWidth = 72,
                    Tag = entry
                };
                button.Click += StartupAction_Click;
                var item = new Grid { Margin = new Thickness(0, 0, 0, 9) };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
                text.Children.Add(new TextBlock
                {
                    Text = entry.Name + (entry.IsDisabled ? " · disabled" : ""),
                    Foreground = (Brush)FindResource("TextPrimary"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                });
                text.Children.Add(new TextBlock
                {
                    Text = entry.Command,
                    Foreground = (Brush)FindResource("TextMuted"),
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = entry.Command,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                Grid.SetColumn(button, 1);
                item.Children.Add(text);
                item.Children.Add(button);
                DetailContent.Children.Add(item);
            }

            if (scan.UnsupportedCount > 0)
            {
                DetailContent.Children.Add(new TextBlock
                {
                    Text = $"{scan.UnsupportedCount} startup entr{(scan.UnsupportedCount == 1 ? "y uses" : "ies use")} an unsupported registry type and were left unchanged.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
        }
        catch (Exception error)
        {
            DetailContent.Children.Add(new TextBlock
            {
                Text = $"Could not read startup entries: {error.Message}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 143, 143)),
                FontSize = 11
            });
        }
    }

    private async void StartupAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: UserStartupEntry entry } button)
        {
            return;
        }

        var message = entry.IsDisabled
            ? $"Restore “{entry.Name}” to the current-user startup list?"
            : $"Disable “{entry.Name}” from starting automatically for this account?\n\nThe original registry value will be saved so you can restore it.";
        if (MessageBox.Show(message, entry.IsDisabled ? "Restore startup item" : "Disable startup item",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            if (entry.IsDisabled)
            {
                await _startup.RestoreAsync(entry);
                StatusText.Text = $"Restored startup entry “{entry.Name}”.";
            }
            else
            {
                await _startup.DisableAsync(entry);
                StatusText.Text = $"Disabled startup entry “{entry.Name}”; its original value is backed up.";
            }

            DetailContent.Children.Clear();
            DetailContent.Children.Add(new TextBlock
            {
                Text = "Current-user startup items",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            await RenderStartupEntriesAsync();
            DetailContent.Children.Add(CreateInstalledAppsFooter());
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Could not change startup item", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void Overclock_Click(object sender, RoutedEventArgs e)
    {
        ShowDetail("Overclocking safety",
            "Direct CPU/GPU clock and voltage changes require a verified vendor-supported backend and live temperature/power telemetry. No such backend is connected in this build, so the controls are intentionally unavailable. Use the utility supplied by your GPU or motherboard manufacturer; Nexus will not write firmware or low-level registers.");
    }

    private void Mouse_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Style = (Style)FindResource("FieldBox"), Width = 190, Text = "Gaming" };
        var dpiBox = new TextBox { Style = (Style)FindResource("FieldBox"), Width = 130, Text = "800" };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new StackPanel
        {
            Children = { new TextBlock { Text = "Profile name", Foreground = (Brush)FindResource("TextMuted"), FontSize = 11, Margin = new Thickness(0, 0, 0, 6) }, nameBox }
        });
        var dpiPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        dpiPanel.Children.Add(new TextBlock { Text = "DPI (100–25,600)", Foreground = (Brush)FindResource("TextMuted"), FontSize = 11, Margin = new Thickness(0, 0, 0, 6) });
        dpiPanel.Children.Add(dpiBox);
        row.Children.Add(dpiPanel);
        var save = new Button { Content = "Save profile", Background = (Brush)FindResource("AccentGradient"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        save.Click += async (_, _) =>
        {
            if (!int.TryParse(dpiBox.Text, out var dpi))
            {
                MessageBox.Show("Enter a whole-number DPI value.", "Invalid DPI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _profiles.SaveAsync(new MouseDpiProfile(nameBox.Text.Trim(), dpi));
                await UpdateProfileCountAsync();
                StatusText.Text = "Mouse profile saved. It is stored as a preset and has not been applied to hardware.";
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Could not save profile", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        var note = new TextBlock
        {
            Text = "Profiles are saved locally. Applying DPI needs a device/vendor API; Windows pointer speed is not the same as mouse DPI, so Nexus will not mislabel or silently apply it.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 11,
            LineHeight = 17,
            Margin = new Thickness(0, 0, 0, 12)
        };
        ShowDetail("Mouse DPI profiles", "Build named presets for different games and tasks.", note, row, save);
    }

    private static void StartUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Could not open Windows Settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
