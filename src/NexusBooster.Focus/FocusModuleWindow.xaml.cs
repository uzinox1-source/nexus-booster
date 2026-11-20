using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NexusBooster.Core.Focus;

namespace NexusBooster.Focus;

public partial class FocusModuleWindow : Window
{
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(3);
    private readonly FocusProgressStore _progress;
    private readonly CancellationTokenSource _critiqueCancellation = new();
    private readonly string _scratchpadPath;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan _remaining;
    private TimeSpan _sessionDuration;
    private bool _sessionRunning;
    private bool _sessionInProgress;
    private bool _sessionFinished;
    private bool _latestCritiquePassed;
    private bool _latestCritiqueCompleted;
    private bool _bossSession;
    private bool _recoverySession;
    private bool _scratchpadDirty;
    private bool _allowClose;
    private bool _critiqueInProgress;
    private DateOnly _sessionDay;
    private FocusProgress? _progressSnapshot;

    public FocusModuleWindow()
    {
        InitializeComponent();
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusBooster",
            "FocusModule");
        _progress = new FocusProgressStore(Path.Combine(dataDirectory, "progress.json"));
        _scratchpadPath = Path.Combine(dataDirectory, "scratchpad.txt");
        _timer.Tick += Timer_Tick;
        StartSessionButton.IsEnabled = false;
        BossSessionButton.IsEnabled = false;
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _progressSnapshot = await _progress.ReadAsync();
            if (File.Exists(_scratchpadPath))
            {
                ScratchpadInput.Text = await File.ReadAllTextAsync(_scratchpadPath);
                _scratchpadDirty = false;
            }

            RefreshProgress();
        }
        catch (Exception error)
        {
            StatusText.Text = $"Focus data could not be loaded: {error.Message}";
            if (_progressSnapshot is not null)
            {
                RefreshProgress();
            }
        }
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        StartSession(TimeSpan.FromMinutes(25), boss: false, recovery: _progressSnapshot?.RecoveryRequired == true);
    }

    private void BossSession_Click(object sender, RoutedEventArgs e)
    {
        if (DateOnly.FromDateTime(DateTime.Today).DayOfWeek != DayOfWeek.Sunday)
        {
            SessionStatusText.Text = "Boss sessions are available on Sunday only.";
            return;
        }

        StartSession(TimeSpan.FromMinutes(90), boss: true, recovery: false);
    }

    private void StartSession(TimeSpan duration, bool boss, bool recovery)
    {
        if (string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            SessionStatusText.Text = "Write the one-line task before starting a block.";
            TaskInput.Focus();
            return;
        }

        if (_sessionInProgress)
        {
            return;
        }

        _sessionDuration = duration;
        _sessionDay = DateOnly.FromDateTime(DateTime.Today);
        _remaining = duration;
        _sessionInProgress = true;
        _bossSession = boss;
        _recoverySession = recovery;
        _sessionRunning = true;
        _sessionFinished = false;
        _latestCritiquePassed = false;
        _latestCritiqueCompleted = false;
        CritiqueResultPanel.Visibility = Visibility.Collapsed;
        _timer.Start();
        UpdateSessionControls();
        UpdateTimerText();
        SessionModeText.Text = recovery ? "25-MINUTE RECOVERY BLOCK" : boss ? "90-MINUTE BOSS SESSION" : "25-MINUTE FOCUS BLOCK";
        SessionStatusText.Text = "In progress. Stay with the one task; the clock pauses after 3 minutes without system input.";
    }

    private void PauseSession_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionRunning)
        {
            _sessionRunning = false;
            _timer.Stop();
            SessionStatusText.Text = $"Paused with {_remaining:mm\\:ss} remaining.";
            UpdateSessionControls();
        }
        else if (_remaining > TimeSpan.Zero && !_sessionFinished)
        {
            _sessionRunning = true;
            _timer.Start();
            SessionStatusText.Text = "Resumed. The idle pause is still active.";
            UpdateSessionControls();
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var idle = GetSystemIdleTime();
            if (idle >= IdleThreshold)
            {
                _remaining = TimeSpan.FromSeconds(Math.Min(
                    _sessionDuration.TotalSeconds,
                    _remaining.TotalSeconds + idle.TotalSeconds));
                _sessionRunning = false;
                _timer.Stop();
                SessionStatusText.Text =
                    $"Paused after {idle.TotalMinutes:0} minutes without input; idle time was removed from the block.";
                UpdateTimerText();
                UpdateSessionControls();
                return;
            }

            _remaining -= TimeSpan.FromSeconds(1);
            if (_remaining <= TimeSpan.Zero)
            {
                _remaining = TimeSpan.Zero;
                _sessionRunning = false;
                _sessionFinished = true;
                _timer.Stop();
                SessionStatusText.Text = "Time complete. Submit this work for a PASS before finalizing the block.";
            }

            UpdateTimerText();
            UpdateSessionControls();
        }
        catch (Win32Exception error)
        {
            _timer.Stop();
            _sessionRunning = false;
            SessionStatusText.Text = $"Idle detection failed; the block was paused: {error.Message}";
            UpdateSessionControls();
        }
    }

    private async void Critique_Click(object sender, RoutedEventArgs e)
    {
        if (_critiqueInProgress || !CanCritique())
        {
            return;
        }

        if (!TryGetMode(out var mode))
        {
            CritiqueStatusText.Text = "Select a critique mode.";
            return;
        }

        _critiqueInProgress = true;
        _latestCritiqueCompleted = false;
        _latestCritiquePassed = false;
        CompleteBlockButton.IsEnabled = false;
        CritiqueButton.IsEnabled = false;
        TaskInput.IsEnabled = false;
        WorkInput.IsEnabled = false;
        ModeSelector.IsEnabled = false;
        CritiqueStatusText.Text = "Sending one request to the local Ollama service…";
        CritiqueResultPanel.Visibility = Visibility.Collapsed;
        try
        {
            using var critique = new LocalCritiqueService();
            var result = await critique.CritiqueAsync(
                mode,
                TaskInput.Text,
                WorkInput.Text,
                _critiqueCancellation.Token);
            _latestCritiqueCompleted = true;
            _latestCritiquePassed = result.Passed;
            CritiqueVerdictText.Text = result.Passed ? "PASS" : "FAIL";
            CritiqueVerdictText.Foreground = new System.Windows.Media.SolidColorBrush(
                result.Passed
                    ? System.Windows.Media.Color.FromRgb(67, 231, 195)
                    : System.Windows.Media.Color.FromRgb(255, 145, 145));
            CritiqueBullets.Children.Clear();
            foreach (var bullet in result.Bullets)
            {
                CritiqueBullets.Children.Add(new TextBlock
                {
                    Text = "• " + bullet,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(203, 209, 222)),
                    FontSize = 11,
                    LineHeight = 16,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            CritiqueResultPanel.Visibility = Visibility.Visible;
            CritiqueStatusText.Text = "One-shot result received from the local model. Edit work and resubmit to try again.";
            UpdateSessionControls();
        }
        catch (OperationCanceledException) when (_critiqueCancellation.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidDataException or ArgumentException)
        {
            _latestCritiqueCompleted = false;
            _latestCritiquePassed = false;
            CritiqueStatusText.Text =
                $"Local critique failed: {error.Message} Ensure Ollama is running and the model is installed. No work was sent to an external service.";
            UpdateSessionControls();
        }
        finally
        {
            _critiqueInProgress = false;
            if (IsVisible)
            {
                TaskInput.IsEnabled = !_sessionInProgress;
                WorkInput.IsEnabled = true;
                ModeSelector.IsEnabled = true;
                CritiqueButton.IsEnabled = CanCritique();
            }
        }
    }

    private async void CompleteBlock_Click(object sender, RoutedEventArgs e)
    {
        if (!_sessionFinished || !_latestCritiqueCompleted || !_latestCritiquePassed)
        {
            return;
        }

        CompleteBlockButton.IsEnabled = false;
        try
        {
            _progressSnapshot = await _progress.RecordFocusBlockAsync(
                (int)_sessionDuration.TotalMinutes,
                _bossSession,
                _recoverySession,
                _sessionDay);
            _sessionInProgress = false;
            _sessionFinished = false;
            SessionStatusText.Text = _bossSession
                ? $"Boss session passed. Permanent rank increased to {_progressSnapshot.Rank}."
                : _recoverySession
                    ? "Recovery session passed. Your saved progress remains intact."
                    : "Focus block passed and added to your local progress.";
            RefreshProgress();
            try
            {
                _progressSnapshot = await _progress.ReadAsync();
                RefreshProgress();
            }
            catch (Exception error)
            {
                StatusText.Text = $"Block recorded; latest progress could not be refreshed: {error.Message}";
            }
        }
        catch (Exception error)
        {
            SessionStatusText.Text = $"Block was not recorded: {error.Message}";
        }
        finally
        {
            UpdateSessionControls();
        }
    }

    private async void LogReading_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var minutes = ParseQuantity(ReadingMinutesInput.Text, 1, 720, "reading minutes");
            _progressSnapshot = await _progress.RecordReadingAsync(minutes);
            StatusText.Text = $"Logged {minutes} minutes of offline reading (self-reported).";
            RefreshProgress();
        }
        catch (Exception error) when (error is ArgumentException or FormatException or IOException)
        {
            StatusText.Text = $"Reading check-in was not recorded: {error.Message}";
        }
    }

    private async void LogBodyEffort_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var amount = ParseQuantity(BodyEffortInput.Text, 1, 10000, "effort amount");
            var effort = (BodyEffortSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
            {
                "PushUps" => BodyEffort.PushUps,
                "Walking" => BodyEffort.Walking,
                _ => throw new InvalidOperationException("Select push-ups or walking.")
            };
            _progressSnapshot = await _progress.RecordBodyEffortAsync(effort, amount);
            StatusText.Text = effort == BodyEffort.PushUps
                ? $"Logged {amount} push-ups (self-reported)."
                : $"Logged {amount} minutes of walking (self-reported).";
            RefreshProgress();
        }
        catch (Exception error) when (error is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            StatusText.Text = $"Body check-in was not recorded: {error.Message}";
        }
    }

    private async void LogShortsFree_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _progressSnapshot = await _progress.RecordShortsFreeDayAsync();
            ShortsFreeButton.IsEnabled = false;
            StatusText.Text = "Saved a self-reported Shorts-free day. Browser activity is not monitored.";
            RefreshProgress();
        }
        catch (Exception error)
        {
            StatusText.Text = $"Willpower check-in was not recorded: {error.Message}";
        }
    }

    private async void SaveScratchpad_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SaveScratchpadAsync();
            StatusText.Text = "Scratchpad saved locally.";
        }
        catch (Exception error)
        {
            StatusText.Text = $"Scratchpad was not saved: {error.Message}";
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        _timer.Stop();
        if (!_scratchpadDirty)
        {
            _critiqueCancellation.Cancel();
            _allowClose = true;
            return;
        }

        e.Cancel = true;
        try
        {
            await SaveScratchpadAsync();
            _allowClose = true;
            _critiqueCancellation.Cancel();
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(
                $"The scratchpad could not be saved, so the Focus Module will stay open.\n\n{error.Message}",
                "Could not save focus notes",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TaskInput_TextChanged(object sender, TextChangedEventArgs e) => InvalidateCritique();
    private void WorkInput_TextChanged(object sender, TextChangedEventArgs e) => InvalidateCritique();
    private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) => InvalidateCritique();
    private void ScratchpadInput_TextChanged(object sender, TextChangedEventArgs e) => _scratchpadDirty = true;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
        {
            if (CanCritique())
            {
                Critique_Click(this, new RoutedEventArgs());
            }

            e.Handled = true;
        }
    }

    private void ColorFilters_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show(
            "Open Windows Color filters settings? Nexus will not change the system-wide filter; choose and toggle it there, then turn it off when finished.",
            "Windows accessibility settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (choice == MessageBoxResult.Yes)
        {
            StartUri("ms-settings:easeofaccess-colorfilter");
        }
    }

    private void OpenExtension_Click(object sender, RoutedEventArgs e)
    {
        var extensionPath = Path.Combine(AppContext.BaseDirectory, "BrowserExtension");
        if (!Directory.Exists(extensionPath))
        {
            MessageBox.Show(
                "The extension files are not beside this build. In the source repository, open src\\NexusBooster.Focus\\BrowserExtension and follow its README.",
                "Browser blocker files",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", extensionPath) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            StatusText.Text = $"Could not open the extension folder: {error.Message}";
        }
    }

    private void InvalidateCritique()
    {
        if (!IsInitialized)
        {
            return;
        }

        _latestCritiqueCompleted = false;
        _latestCritiquePassed = false;
        CritiqueResultPanel.Visibility = Visibility.Collapsed;
        CritiqueButton.IsEnabled = CanCritique();
        UpdateSessionControls();
    }

    private bool CanCritique() =>
        !string.IsNullOrWhiteSpace(TaskInput.Text) &&
        !string.IsNullOrWhiteSpace(WorkInput.Text) &&
        ModeSelector.SelectedItem is ComboBoxItem &&
        !_critiqueInProgress;

    private bool TryGetMode(out CritiqueMode mode)
    {
        mode = (ModeSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "CodeAuditor" => CritiqueMode.CodeAuditor,
            "LogicHunter" => CritiqueMode.LogicHunter,
            "BloatCutter" => CritiqueMode.BloatCutter,
            "GoalAlignment" => CritiqueMode.GoalAlignment,
            _ => (CritiqueMode)(-1)
        };
        return Enum.IsDefined(mode);
    }

    private void UpdateSessionControls()
    {
        StartSessionButton.IsEnabled = _progressSnapshot is not null && !_sessionInProgress;
        BossSessionButton.IsEnabled =
            !_sessionInProgress &&
            _progressSnapshot?.RecoveryRequired != true &&
            DateOnly.FromDateTime(DateTime.Today).DayOfWeek == DayOfWeek.Sunday;
        TaskInput.IsEnabled = !_sessionInProgress && !_critiqueInProgress;
        PauseSessionButton.IsEnabled = _sessionRunning || (_remaining > TimeSpan.Zero && !_sessionFinished);
        PauseSessionButton.Content = _sessionRunning ? "Pause" : "Resume";
        CompleteBlockButton.IsEnabled = _sessionFinished && _latestCritiqueCompleted && _latestCritiquePassed;
        CritiqueButton.IsEnabled = CanCritique();
        RecoveryBadge.Visibility = _progressSnapshot?.RecoveryRequired == true ? Visibility.Visible : Visibility.Collapsed;
        ShortsFreeButton.IsEnabled = _progressSnapshot?.Today.ShortsFree != true;
        if (_progressSnapshot?.RecoveryRequired == true)
        {
            SessionModeText.Text = "25-MINUTE RECOVERY BLOCK";
            StartSessionButton.Content = "Start recovery · 25 min";
        }
        else
        {
            SessionModeText.Text = _bossSession ? "90-MINUTE BOSS SESSION" : "25-MINUTE FOCUS BLOCK";
            StartSessionButton.Content = "Start 25 min";
        }
    }

    private void RefreshProgress()
    {
        if (_progressSnapshot is null)
        {
            return;
        }

        FocusStatText.Text = $"{_progressSnapshot.TotalFocusMinutes} min";
        IntellectStatText.Text = $"{_progressSnapshot.IntellectMinutes} min";
        BodyStatText.Text = $"{_progressSnapshot.PushUpReps} reps · {_progressSnapshot.WalkingMinutes} min";
        WillpowerStatText.Text = $"{_progressSnapshot.ShortsFreeDays} days";
        RankText.Text = $"Rank {_progressSnapshot.Rank} · boss sessions completed: {_progressSnapshot.Rank}";
        QuestStatusText.Text = _progressSnapshot.IsQuestComplete
            ? "QUEST COMPLETE · Your local break eligibility is unlocked. Nexus does not lock or unlock other apps."
            : $"Today: {_progressSnapshot.Today.FocusMinutes}/25 focus min, {_progressSnapshot.Today.PushUpReps}/20 push-ups, {_progressSnapshot.Today.ReadingMinutes}/15 reading min. Complete the focus block and either IRL check-in.";
        UpdateSessionControls();
    }

    private void UpdateTimerText() =>
        TimerText.Text = $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";

    private async Task SaveScratchpadAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_scratchpadPath)!);
        await File.WriteAllTextAsync(_scratchpadPath, ScratchpadInput.Text);
        _scratchpadDirty = false;
    }

    private static int ParseQuantity(string text, int minimum, int maximum, string description)
    {
        if (!int.TryParse(text, out var value) || value < minimum || value > maximum)
        {
            throw new ArgumentException($"Enter {description} from {minimum} to {maximum}.");
        }

        return value;
    }

    private static TimeSpan GetSystemIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    private static void StartUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Could not open Windows settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
