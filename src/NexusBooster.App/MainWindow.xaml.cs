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
    private StackPanel? _tweakBrowserList;
    private ComboBox? _tweakCategorySelector;

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
        if (!isApplied && !string.IsNullOrWhiteSpace(tweak.Warning) &&
            MessageBox.Show(
                $"WARNING — {tweak.Risk} risk\n\n{tweak.Warning}\n\nContinue to the separate apply confirmation?",
                $"Warning: {tweak.Name}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

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
            RenderTweakCategory();
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
        TweakBrowser_Click(sender, e);
    }

    private void TweakBrowser_Click(object sender, RoutedEventArgs e)
    {
        var warning = new TextBlock
        {
            Text = "Every change is optional. Read the effect and warning before applying; moderate-risk changes show a separate warning and confirmation. Unsupported or risky system-wide changes are not automated.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
            FontSize = 11,
            LineHeight = 17,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _tweakCategorySelector = new ComboBox
        {
            ItemsSource = new[]
            {
                TweakCategories.Fps,
                TweakCategories.Ping,
                TweakCategories.Power,
                TweakCategories.Privacy,
                TweakCategories.Ui,
                "Mouse & Input",
                TweakCategories.Maintenance
            },
            SelectedIndex = 0,
            MinWidth = 230,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(9, 7, 9, 7),
            Background = new SolidColorBrush(Color.FromRgb(27, 32, 44)),
            Foreground = new SolidColorBrush(Color.FromRgb(243, 244, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 64, 82)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        _tweakBrowserList = new StackPanel();
        _tweakCategorySelector.SelectionChanged += (_, _) => RenderTweakCategory();
        ShowDetail(
            "Tweak categories",
            "Browse practical settings by topic. The FPS and Ping categories do not promise a performance gain; measure changes in the games and network you actually use.",
            warning,
            _tweakCategorySelector,
            _tweakBrowserList);
        RenderTweakCategory();
    }

    private void RenderTweakCategory()
    {
        if (_tweakBrowserList is null || _tweakCategorySelector?.SelectedItem is not string category)
        {
            return;
        }

        _tweakBrowserList.Children.Clear();
        var definitions = category == "Mouse & Input"
            ? TweakCatalog.All.Where(tweak => tweak.Id == "mouse-acceleration").ToArray()
            : category == TweakCategories.Privacy
                ? []
                : TweakCatalog.All.Where(tweak => string.Equals(tweak.Category, category, StringComparison.Ordinal)).ToArray();

        foreach (var tweak in definitions)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(Color.FromRgb(27, 32, 44)),
                Padding = new Thickness(13),
                Margin = new Thickness(0, 0, 0, 9)
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            text.Children.Add(new TextBlock
            {
                Text = tweak.Risk == "Low" ? tweak.Name : $"{tweak.Name} · {tweak.Risk} risk",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                Foreground = (Brush)FindResource("TextPrimary")
            });
            text.Children.Add(new TextBlock
            {
                Text = tweak.Description + " " + tweak.RestartNote,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10.5,
                LineHeight = 16,
                Foreground = (Brush)FindResource("TextMuted"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            if (!string.IsNullOrWhiteSpace(tweak.Warning))
            {
                text.Children.Add(new TextBlock
                {
                    Text = $"WARNING: {tweak.Warning}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 10,
                    LineHeight = 15,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
                    Margin = new Thickness(0, 7, 0, 0)
                });
            }
            var applied = _tweaks.IsApplied(tweak.Id);
            var action = new Button
            {
                Content = applied ? "Undo" : "Review",
                Background = applied
                    ? new SolidColorBrush(Color.FromRgb(52, 73, 70))
                    : new SolidColorBrush(Color.FromRgb(41, 47, 62)),
                Padding = new Thickness(12, 7, 12, 7),
                MinWidth = 66,
                Tag = tweak,
                VerticalAlignment = VerticalAlignment.Center
            };
            action.Click += TweakAction_Click;
            Grid.SetColumn(action, 1);
            layout.Children.Add(text);
            layout.Children.Add(action);
            card.Child = layout;
            _tweakBrowserList.Children.Add(card);
        }

        if (definitions.Length > 0)
        {
            return;
        }

        var guidance = category switch
        {
            TweakCategories.Ping => "Ping is mainly determined by your ISP route, game server, congestion, and Wi-Fi quality. Registry “ping fixes” cannot shorten that route. Compare the same server and time of day, test Ethernet versus Wi-Fi, pause large downloads, and check for packet loss before changing settings.",
            TweakCategories.Power => "USB selective suspend and PCIe Link State Power Management trade power use and heat for lower idle consumption. Results vary by device; changing them globally can reduce battery life or affect sleep/wake behavior. Review Windows Settings or your PC/motherboard maker's guidance manually. Nexus does not change hidden power settings, device power policies, or disable hardware.",
            TweakCategories.Privacy => "Keep Windows Security, Defender, Firewall, and Windows Update enabled. Avoid blocking vendor or Microsoft domains through the hosts file or disabling diagnostic/update services; those changes can break updates, support, or security. This section is guidance-only; Nexus does not change policy or security settings.",
            TweakCategories.Maintenance => "Use Windows Storage settings to review temporary files before removing them. Nexus will not purge Prefetch, shader caches, update downloads, or WinSxS; indiscriminate cleanup can slow apps, remove rollback data, or damage servicing. DISM/SFC repair should be run only for a diagnosed Windows problem.",
            _ => "This topic is guidance-only because the commonly shared tweaks can impair networking, hardware power behavior, security, or Windows servicing."
        };
        _tweakBrowserList.Children.Add(new TextBlock
        {
            Text = guidance,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 11,
            LineHeight = 18,
            Margin = new Thickness(0, 0, 0, 12)
        });

        if (category == TweakCategories.Power)
        {
            AddSettingsButton(_tweakBrowserList, "Open Power & battery settings", "ms-settings:powersleep");
            AddSettingsButton(_tweakBrowserList, "Open USB settings", "ms-settings:usb");
        }
        else if (category == TweakCategories.Maintenance)
        {
            AddSettingsButton(_tweakBrowserList, "Open Storage settings", "ms-settings:storagesense");
        }
        else if (category == TweakCategories.Ping)
        {
            AddSettingsButton(_tweakBrowserList, "Open Network status settings", "ms-settings:network-status");
        }
    }

    private static void AddSettingsButton(Panel destination, string label, string uri)
    {
        var button = new Button
        {
            Content = label + "  →",
            Background = new SolidColorBrush(Color.FromRgb(36, 42, 55)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            Tag = uri
        };
        button.Click += (_, _) => StartUri(uri);
        destination.Children.Add(button);
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
            : $"WARNING — Disabling a startup item may stop its background sync, notifications, or updater. Only disable items you recognize.\n\nContinue to the separate confirmation for “{entry.Name}”?";
        if (MessageBox.Show(message, entry.IsDisabled ? "Restore startup item" : "Disable startup item",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!entry.IsDisabled &&
            MessageBox.Show(
                $"Disable “{entry.Name}” from starting automatically for this account?\n\nThe original registry value will be saved so you can restore it.",
                "Confirm startup change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
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

    private void FpsAdvisor_Click(object sender, RoutedEventArgs e)
    {
        var note = new TextBlock
        {
            Text = "This is a local, rule-based advisor—not a connected AI model. It uses only the choices below; it does not scan your PC or game. Suggestions are starting points, not promised FPS gains.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
            FontSize = 11,
            LineHeight = 17,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var bottleneck = CreateChoice(
            "What seems to limit performance?",
            ["Not sure", "GPU is near full use", "CPU is near full use"]);
        var resolution = CreateChoice("Game resolution", ["1920 × 1080", "2560 × 1440", "3840 × 2160", "Other"]);
        var priority = CreateChoice("Your priority", ["Smoother competitive play", "Higher visual quality", "Balanced"]);
        var output = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        var advise = new Button
        {
            Content = "Get local recommendations",
            Background = (Brush)FindResource("AccentGradient"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 14, 0, 0)
        };
        advise.Click += (_, _) => RenderFpsAdvice(output, bottleneck, resolution, priority);

        ShowDetail("AI FPS advisor",
            "Choose what you observe in a game. Change one graphics setting at a time, then compare the same scene using the game's benchmark or an FPS overlay.",
            note,
            CreateChoiceRow(bottleneck, resolution, priority),
            advise,
            output);
    }

    private static ComboBox CreateChoice(string label, IEnumerable<string> choices)
    {
        var combo = new ComboBox
        {
            ItemsSource = choices,
            SelectedIndex = 0,
            MinWidth = 180,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(27, 32, 44)),
            Foreground = new SolidColorBrush(Color.FromRgb(243, 244, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 64, 82)),
            Tag = label,
            Margin = new Thickness(0, 0, 10, 10)
        };
        return combo;
    }

    private static StackPanel CreateChoiceRow(params ComboBox[] choices)
    {
        var row = new StackPanel();
        foreach (var choice in choices)
        {
            row.Children.Add(new TextBlock
            {
                Text = (string)choice.Tag + "  ·",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(155, 163, 181)),
                Margin = new Thickness(0, 6, 0, 4)
            });
            row.Children.Add(choice);
        }

        return row;
    }

    private static void RenderFpsAdvice(
        Panel output,
        ComboBox bottleneck,
        ComboBox resolution,
        ComboBox priority)
    {
        output.Children.Clear();
        var selectedBottleneck = bottleneck.SelectedItem?.ToString() ?? "Not sure";
        var selectedResolution = resolution.SelectedItem?.ToString() ?? "Other";
        var selectedPriority = priority.SelectedItem?.ToString() ?? "Balanced";
        var recommendations = new List<(string Title, string Body)>();

        if (selectedBottleneck == "GPU is near full use")
        {
            recommendations.Add(("If GPU-limited",
                $"At {selectedResolution}, test a modest render-scale reduction or lower one GPU-heavy option (such as ray tracing, shadows, or volumetrics). These settings mainly reduce GPU work; keep an eye on image quality."));
        }
        else if (selectedBottleneck == "CPU is near full use")
        {
            recommendations.Add(("If CPU-limited",
                "Try reducing CPU-heavy options such as crowd density, simulation, or view distance. Close only background apps you recognize and do not need. Lowering texture quality often will not help a CPU bottleneck."));
        }
        else
        {
            recommendations.Add(("First identify the limit",
                "Check GPU utilization and per-core CPU load during a repeatable scene. If GPU use stays near full, test a GPU-heavy setting; if one or more CPU cores are saturated while GPU use is lower, test a CPU-heavy setting. Avoid changing many options at once."));
        }

        if (selectedPriority == "Smoother competitive play")
        {
            recommendations.Add(("For smoother play",
                "Prefer stable frame times over a fluctuating peak FPS. If the game supports it, test an FPS cap your PC can sustain near your display refresh rate. Check whether VRR (FreeSync/G-SYNC Compatible) is supported; V-Sync can reduce tearing but may add latency."));
        }
        else if (selectedPriority == "Higher visual quality")
        {
            recommendations.Add(("For visual quality",
                "Keep native resolution as a baseline. If supported by the game and GPU, compare its built-in upscaling quality modes and change one option at a time; inspect for blur or artifacts as well as frame rate."));
        }
        else
        {
            recommendations.Add(("For a balanced profile",
                "Keep native resolution initially, then adjust one costly graphics option at a time. Use built-in upscaling only if the game supports it and compare image quality and frame times."));
        }

        recommendations.Add(("Measure before and after",
            "Use the same game scene or built-in benchmark for each comparison. Driver updates do not guarantee higher FPS; install them from your GPU or device maker only when appropriate."));

        foreach (var recommendation in recommendations)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(27, 32, 44)),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 9)
            };
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = recommendation.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("TextPrimary")
            });
            content.Children.Add(new TextBlock
            {
                Text = recommendation.Body,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 17,
                Foreground = (Brush)Application.Current.FindResource("TextMuted"),
                Margin = new Thickness(0, 5, 0, 0)
            });
            card.Child = content;
            output.Children.Add(card);
        }
    }

    private void Drivers_Click(object sender, RoutedEventArgs e)
    {
        var warning = new TextBlock
        {
            Text = "Nexus never downloads or installs drivers. Confirm your exact GPU and Windows version on the vendor site. For laptops or prebuilt PCs, the computer maker may provide a tested driver; MSI, ASUS, and Gigabyte pages require the exact product model.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 107)),
            FontSize = 11,
            LineHeight = 17,
            Margin = new Thickness(0, 0, 0, 14)
        };

        ShowDetail("Official GPU drivers",
            "Open the graphics-chip maker's official download page, or your PC/GPU board maker's official support page.",
            warning,
            CreateDriverButton("AMD Radeon · official downloads", "https://www.amd.com/en/support/download/drivers.html"),
            CreateDriverButton("NVIDIA GeForce · official downloads", "https://www.nvidia.com/Download/index.aspx"),
            CreateDriverButton("Intel Graphics · official downloads", "https://www.intel.com/content/www/us/en/download-center/home.html"),
            new TextBlock
            {
                Text = "Board and computer manufacturers",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                Margin = new Thickness(0, 16, 0, 8)
            },
            CreateDriverButton("MSI · official support & downloads", "https://www.msi.com/support/download"),
            CreateDriverButton("ASUS · official support", "https://www.asus.com/support/"),
            CreateDriverButton("Gigabyte · official support", "https://www.gigabyte.com/Support"));
    }

    private Button CreateDriverButton(string label, string url)
    {
        var button = new Button
        {
            Content = label + "  ↗",
            Background = new SolidColorBrush(Color.FromRgb(36, 42, 55)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            Tag = url
        };
        button.Click += (_, _) => StartUri(url);
        return button;
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
