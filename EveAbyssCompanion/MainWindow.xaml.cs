using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace EveAbyssCompanion
{
    // ============================================================
    // =================== MainWindow class START ==================
    // ============================================================
    public partial class MainWindow : Window
    {
        // ===== Core timer & session state =====
        private readonly DispatcherTimer _timer;
        private TimeSpan _remaining;
        private bool _sessionActive;
        private DateTime _sessionStart;
        private DispatcherTimer _autoClearDetectedTimer;

        // Run finished but not submitted yet
        private bool _pendingSubmit;
        private DateTime _finishTimestamp;
        private TimeSpan _finishElapsed;
        private TimeSpan _finishRemaining;

        // Room tracking
        private int _currentRoom; // 0 = none, 1..3

        // Room split timing
        private DateTime? _room1Start;
        private DateTime? _room2Start;
        private DateTime? _room3Start;
        private TimeSpan _room1Time;
        private TimeSpan _room2Time;
        private TimeSpan _room3Time;

        // For overlay "pressure color"
        private DateTime _currentRoomStart;

        // Flag reminder
        private bool _dronesNeedRepair;

        // Session loot — single source of truth for Before/After values
        private string? _sessionLootBefore;
        private string? _sessionLootAfter;

        // Selections
        private string _selectedTier = string.Empty;
        private string _selectedWeather = string.Empty;

        // Overlay
        private OverlayWindow? _overlay;

        // Combat log NPC auto-detect (optional)
        private readonly AppConfig _config;
        private CombatLogMonitor? _combatLogMonitor;
        private readonly DispatcherTimer _combatLogTimer;

        // History
        // Store in AppData so history persists even if the app is run/built from different folders.
        // This fixes the common "History is empty" issue when the working directory changes.
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EveAbyssCompanion");

        private static string HistoryFilePath
        {
            get
            {
                try { Directory.CreateDirectory(AppDataDir); } catch { /* ignore */ }
                return Path.Combine(AppDataDir, "session_history.json");
            }
        }

        // NPC library
        private List<NpcEntry> _npcAll = new();
        private List<NpcEntry> _npcFiltered = new();
        private bool _detectedMode;

        // ===== Window placement (multi-monitor) =====
        // NOTE: We intentionally avoid WinForms in this project to keep namespaces clean.
        // We use user32 EnumDisplayMonitors/GetMonitorInfo and convert device pixels -> WPF DIPs.

        private const int MONITORINFOF_PRIMARY = 0x00000001;

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
        private HashSet<string> _detectedNames = new(StringComparer.OrdinalIgnoreCase);

        // ===== Visual constants =====
        private static readonly Brush NormalBorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x42, 0x52));
        private static readonly Brush AccentBorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0xA6, 0xFF));
        private static readonly Brush SuccessBorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xFF, 0xB3));

        public MainWindow()
        {
            InitializeComponent();

            // App config (stored in %AppData%)
            _config = AppConfig.Load();

            // First-time setup wizard
            if (!_config.SetupComplete)
            {
                var setup = new SetupWindow(_config);
                setup.ShowDialog();
                // Config already saved by wizard — reload fresh
                _config.CockpitMode           = _config.CockpitMode;
                _config.EnableCombatLogMonitor = _config.EnableCombatLogMonitor;
            }

            // Window placement (multi-monitor)
            StartOnSecondMonitorCheckBox.IsChecked = _config.StartOnSecondMonitor;
            StartOnSecondMonitorCheckBox.Checked += StartOnSecondMonitorCheckBox_Changed;
            StartOnSecondMonitorCheckBox.Unchecked += StartOnSecondMonitorCheckBox_Changed;
            Loaded += MainWindow_Loaded;

            EnableCombatLogCheckBox.IsChecked = _config.EnableCombatLogMonitor;
            CombatLogPathText.Text = $"{_config.CombatLogFolder}";
            EnableCombatLogCheckBox.Checked += EnableCombatLogCheckBox_Changed;
            EnableCombatLogCheckBox.Unchecked += EnableCombatLogCheckBox_Changed;

            ReloadNpcDatasetButton.Click += ReloadNpcDatasetButton_Click;

            // Cockpit mode
            CockpitModeCheckBox.IsChecked = _config.CockpitMode;
            CockpitModeCheckBox.Checked   += CockpitModeCheckBox_Changed;
            CockpitModeCheckBox.Unchecked += CockpitModeCheckBox_Changed;
            ApplyCockpitMode(_config.CockpitMode);

            _combatLogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _combatLogTimer.Tick += CombatLogTimer_Tick;

            // Testing QoL: clear detected list every 30 seconds (only affects Detected view)
            _autoClearDetectedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoClearDetectedTimer.Tick += AutoClearDetectedTimer_Tick;
            _autoClearDetectedTimer.Start();

            // HARD FIX #2: overlay must die with the app
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            ResetTimer();

            LoadNpcLibrary();
            LoadHistoryIntoUi();
            RecalcStats();

            UpdateSelectionHighlights();
            UpdateRoomButtonsVisual();
            UpdateRunStateText();
            UpdateSubmitButtonVisual();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save window placement
            try
            {
                SaveWindowPlacementToConfig();
                _config.Save();
            }
            catch { /* ignore */ }

            // Ensure overlay is closed even if AlwaysOverlay is checked
            try { _overlay?.Close(); } catch { /* ignore */ }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Safety: force close
            try { _overlay?.Close(); } catch { /* ignore */ }
            _overlay = null;
        }

        private void StartOnSecondMonitorCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.StartOnSecondMonitor = StartOnSecondMonitorCheckBox.IsChecked == true;
            _config.Save();

            // Re-apply immediately so you can see it without restarting.
            ApplyWindowPlacementFromConfig(forceSecondMonitor: _config.StartOnSecondMonitor);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowPlacementFromConfig(forceSecondMonitor: _config.StartOnSecondMonitor);
        }

        private void ApplyWindowPlacementFromConfig(bool forceSecondMonitor)
        {
            // Delay until layout is ready (prevents weird sizing on first paint).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 1) Prefer saved placement if enabled and valid
                    if (_config.RememberWindowPlacement)
                    {
                        var saved = new Rect(_config.WindowLeft, _config.WindowTop, _config.WindowWidth, _config.WindowHeight);
                        if (!double.IsNaN(saved.Left) && !double.IsNaN(saved.Top) && saved.Width > 200 && saved.Height > 200 && IsRectOnAnyScreen(saved))
                        {
                            WindowState = WindowState.Normal;
                            Left = saved.Left;
                            Top = saved.Top;
                            Width = saved.Width;
                            Height = saved.Height;
                            if (_config.WindowMaximized) WindowState = WindowState.Maximized;
                            return;
                        }
                    }

                    // 2) Otherwise place on 2nd monitor if requested and available
                    if (forceSecondMonitor)
                    {
			            var wa = GetTargetWorkingAreaDip(preferSecondary: true);
			            MoveToWorkingAreaDip(wa, maximize: true);
                    }
                }
                catch { /* ignore */ }
            }), DispatcherPriority.ApplicationIdle);
        }

        private void SaveWindowPlacementToConfig()
        {
            if (!_config.RememberWindowPlacement) return;

            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

            _config.WindowLeft = bounds.Left;
            _config.WindowTop = bounds.Top;
            _config.WindowWidth = bounds.Width;
            _config.WindowHeight = bounds.Height;
            _config.WindowMaximized = WindowState == WindowState.Maximized;
        }

        private static bool IsRectOnAnyScreen(Rect rectDip)
        {
            // Simple safety check using the virtual desktop bounds.
            // If the saved position is way outside, we snap the window back.
            var virtualDip = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            return virtualDip.IntersectsWith(rectDip);
        }

        private Rect GetTargetWorkingAreaDip(bool preferSecondary)
        {
            // Enumerate monitors via Win32 so we don't need Windows Forms.
            var monitors = MonitorHelper.GetMonitors();
            var chosen = preferSecondary
                ? monitors.FirstOrDefault(m => !m.IsPrimary) ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault()
                : monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();

            if (chosen == null)
            {
                // Fallback: primary work area
                return SystemParameters.WorkArea;
            }

            // Convert px -> DIP using the current window's transform.
            // After Loaded(), PresentationSource is available.
            var src = PresentationSource.FromVisual(this);
            var fromDevice = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

            var topLeft = fromDevice.Transform(new System.Windows.Point(chosen.PixelWorkArea.Left, chosen.PixelWorkArea.Top));
            var bottomRight = fromDevice.Transform(new System.Windows.Point(chosen.PixelWorkArea.Right, chosen.PixelWorkArea.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private void MoveToWorkingAreaDip(Rect workingAreaDip, bool maximize)
        {
            WindowState = WindowState.Normal;

            Left = workingAreaDip.Left;
            Top = workingAreaDip.Top;
            Width = workingAreaDip.Width;
            Height = workingAreaDip.Height;

            if (maximize)
            {
                WindowState = WindowState.Maximized;
            }
        }

        // ================= Overlay helpers =================
        private void EnsureOverlay()
        {
            if (_overlay != null) return;

            _overlay = new OverlayWindow(
                onStartClicked:    () => StartButton_Click(this, new RoutedEventArgs()),
                onSubmitClicked:   () => LootSubmitButton_Click(this, new RoutedEventArgs()),
                onEndNowClicked:   () => EndSession("Overlay End Now"),
                onSetupClicked:    () =>
                {
                    WindowState = WindowState.Normal;
                    Show();
                    Activate();
                    Focus();
                },
                onRoomDoneClicked: room => MarkRoomDone(room),
                onTierSelected:    tier => SetTierFromOverlay(tier),
                onWeatherSelected: weather => SetWeatherFromOverlay(weather),
                onToggleDroneArmor: () => ToggleDroneArmorFlag(),
                onDroneRepaired:   () => ClearDroneArmorFlag(),
                onClearDetected:   () => { ClearDetectedMode(); ApplyNpcFilterAndRefresh(); UpdateOverlayNpcs(); }
            );

            // Sync overlay loot fields → main window fields
            _overlay.InvChanged += (start, end) =>
            {
                if (MainInvStartTextBox != null && MainInvStartTextBox.Text != start)
                    MainInvStartTextBox.Text = start;
                if (MainInvEndTextBox != null && MainInvEndTextBox.Text != end)
                    MainInvEndTextBox.Text = end;
            };

            // Push any values already typed in main window into the overlay
            _overlay.SyncInvStart(MainInvStartTextBox?.Text ?? "");
            _overlay.SyncInvEnd(MainInvEndTextBox?.Text ?? "");

            // No Owner — keeps overlay independent so minimizing main window
            // does NOT minimize the overlay. Topmost handles always-on-top instead.
        }

        private void ShowOverlay()
        {
            EnsureOverlay();
            _overlay!.Show();
            _overlay.Topmost = true;
            UpdateOverlay();
        }

        private void HideOverlayIfNotAlwaysOn()
        {
            // In cockpit mode the overlay is the primary control — never hide it
            if (_config.CockpitMode) return;
            if (AlwaysOverlayCheckBox.IsChecked == true) return;
            _overlay?.Hide();
        }

        private void UpdateOverlay()
        {
            if (_overlay == null) return;

            var displayRemaining = _pendingSubmit ? _finishRemaining : _remaining;

            TimeSpan currentRoomElapsed = TimeSpan.Zero;
            if (_sessionActive && _currentRoom >= 1 && _currentRoom <= 3)
            {
                currentRoomElapsed = DateTime.Now - _currentRoomStart;
                if (currentRoomElapsed < TimeSpan.Zero) currentRoomElapsed = TimeSpan.Zero;
            }

            _overlay.UpdateDisplay(
                remaining: displayRemaining,
                currentRoom: _currentRoom,
                selectedTier: _selectedTier,
                selectedWeather: _selectedWeather,
                isRunning: _sessionActive,
                isPendingSubmit: _pendingSubmit,
                currentRoomElapsed: currentRoomElapsed,
                dronesNeedRepair: _dronesNeedRepair
            );
        }

        private void UpdateOverlayNpcs()
        {
            if (_overlay == null) return;
            if (_detectedMode && _detectedNames.Count > 0)
            {
                var detected = _npcAll
                    .Where(n => _detectedNames.Contains(n.Name))
                    .OrderBy(n => n.KillPriority == "First" ? 0 : n.KillPriority == "Early" ? 1 : 2)
                    .ToList();
                _overlay.UpdateDetectedNpcs(detected);
            }
            else
            {
                _overlay.UpdateDetectedNpcs(new List<NpcEntry>());
            }
        }

        // ================= UI helpers =================
        private void UpdateTimerDisplay()
        {
            var displayRemaining = _pendingSubmit ? _finishRemaining : _remaining;
            var formatted = displayRemaining.ToString(@"mm\:ss");
            if (TimerText != null) TimerText.Text = formatted;
            if (CockpitTimerText != null) CockpitTimerText.Text = formatted;
        }

        private void UpdateRoomLabel()
        {
            RoomText.Text = _currentRoom <= 0 ? "Room: 0 / 3" : $"Room: {_currentRoom} / 3";
        }

        private void SetHistoryStatus(string? message)
        {
            if (HistoryStatusTextBlock != null)
                HistoryStatusTextBlock.Text = message ?? string.Empty;
        }

        private void UpdateRunStateText()
        {
            if (RunStateTextBlock == null) return;

            if (_pendingSubmit) RunStateTextBlock.Text = "FINISHED • Pending Submit";
            else if (_sessionActive) RunStateTextBlock.Text = $"RUNNING • Room {_currentRoom}";
            else RunStateTextBlock.Text = "READY";
        }

        private void UpdateSubmitButtonVisual()
        {
            if (SubmitEndButton == null) return;

            if (_pendingSubmit)
            {
                SubmitEndButton.BorderBrush = SuccessBorderBrush;
                SubmitEndButton.BorderThickness = new Thickness(2);
                SubmitEndButton.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                SubmitEndButton.BorderBrush = SuccessBorderBrush;
                SubmitEndButton.BorderThickness = new Thickness(1);
                SubmitEndButton.FontWeight = FontWeights.Normal;
            }
        }

        private void UpdateSelectionHighlights()
        {
            HighlightSelectedButtonInPanel(TierButtonsGrid, _selectedTier);
            HighlightSelectedButtonInPanel(WeatherButtonsGrid, _selectedWeather);
        }

        private void HighlightSelectedButtonInPanel(Panel? panel, string selectedTag)
        {
            if (panel == null) return;

            foreach (var btn in panel.Children.OfType<Button>())
            {
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = NormalBorderBrush;
                btn.FontWeight = FontWeights.Normal;
            }

            if (string.IsNullOrWhiteSpace(selectedTag)) return;

            foreach (var btn in panel.Children.OfType<Button>())
            {
                if (btn.Tag is string tag && string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase))
                {
                    btn.BorderThickness = new Thickness(2);
                    btn.BorderBrush = AccentBorderBrush;
                    btn.FontWeight = FontWeights.SemiBold;
                    break;
                }
            }
        }

        private void UpdateRoomButtonsVisual()
        {
            if (MainR1DoneButton == null || MainR2DoneButton == null || MainR3DoneButton == null) return;

            ResetRoomButton(MainR1DoneButton);
            ResetRoomButton(MainR2DoneButton);
            ResetRoomButton(MainR3DoneButton);

            if (_pendingSubmit)
            {
                MarkRoomDoneVisual(MainR1DoneButton);
                MarkRoomDoneVisual(MainR2DoneButton);
                MarkRoomDoneVisual(MainR3DoneButton);
                return;
            }

            if (!_sessionActive) return;

            if (_currentRoom >= 2) MarkRoomDoneVisual(MainR1DoneButton);
            if (_currentRoom >= 3) MarkRoomDoneVisual(MainR2DoneButton);

            if (_currentRoom == 1) MarkRoomCurrentVisual(MainR1DoneButton);
            if (_currentRoom == 2) MarkRoomCurrentVisual(MainR2DoneButton);
            if (_currentRoom == 3) MarkRoomCurrentVisual(MainR3DoneButton);
        }

        private void ResetRoomButton(Button b)
        {
            b.BorderThickness = new Thickness(1);
            b.BorderBrush = NormalBorderBrush;
            b.FontWeight = FontWeights.Normal;
        }

        private void MarkRoomDoneVisual(Button b)
        {
            b.BorderThickness = new Thickness(2);
            b.BorderBrush = SuccessBorderBrush;
            b.FontWeight = FontWeights.SemiBold;
        }

        private void MarkRoomCurrentVisual(Button b)
        {
            b.BorderThickness = new Thickness(2);
            b.BorderBrush = _roomFlashState ? AccentBorderBrush : NormalBorderBrush;
            b.FontWeight = FontWeights.SemiBold;
        }

        // ================ Timer core ==================
        private void ResetTimer()
        {
            _timer.Stop();
            _remaining = TimeSpan.FromMinutes(20);
            _sessionActive = false;

            _pendingSubmit = false;
            _finishTimestamp = DateTime.MinValue;
            _finishElapsed = TimeSpan.Zero;
            _finishRemaining = TimeSpan.Zero;

            _currentRoom = 0;

            _room1Start = _room2Start = _room3Start = null;
            _room1Time = _room2Time = _room3Time = TimeSpan.Zero;

            _currentRoomStart = DateTime.Now;
            // Note: _dronesNeedRepair intentionally NOT reset here
            // It persists until player confirms repair via the Repaired button
            // Only clears on explicit ClearDroneArmorFlag() or new run start

            UpdateTimerDisplay();
            UpdateRoomLabel();
            UpdateRunStateText();
            UpdateSelectionHighlights();
            UpdateRoomButtonsVisual();
            UpdateSubmitButtonVisual();
            UpdateOverlay();
        }

        private bool _roomFlashState = false;

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_sessionActive) return;

            _remaining -= TimeSpan.FromSeconds(1);

            if (_remaining <= TimeSpan.Zero)
            {
                _remaining = TimeSpan.Zero;
                UpdateTimerDisplay();
                EndSession("Time up");
                return;
            }

            _roomFlashState = !_roomFlashState;
            UpdateRoomButtonsVisual();
            UpdateTimerDisplay();
            UpdateOverlay();
        }

        // ============ Session lifecycle ===============
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sessionActive) return;

            if (_pendingSubmit)
            {
                MessageBox.Show(
                    "This run is finished but not submitted yet.\n\nClick Submit & End to save it, or Reset to discard.",
                    "Pending Submit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(_selectedTier) || string.IsNullOrEmpty(_selectedWeather))
            {
                MessageBox.Show(
                    "Please select a Tier and Weather before starting.",
                    "Missing selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _sessionActive = true;
            _sessionStart = DateTime.Now;
            _remaining = TimeSpan.FromMinutes(20);

            _currentRoom = 1;

            var now = DateTime.Now;
            _room1Start = now;
            _room2Start = _room3Start = null;

            _room1Time = _room2Time = _room3Time = TimeSpan.Zero;
            _currentRoomStart = now;

            // _dronesNeedRepair intentionally NOT reset here — persists until
            // the player hits the Repaired button. Cleared only by ClearDroneArmorFlag().

            SetHistoryStatus(string.Empty);

            UpdateTimerDisplay();
            UpdateRoomLabel();
            UpdateRunStateText();
            UpdateSelectionHighlights();
            UpdateRoomButtonsVisual();
            UpdateSubmitButtonVisual();

            ShowOverlay();

            StartCombatLogMonitorIfEnabled();
            _timer.Start();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Reset current run? This will not save it to history.",
                    "Reset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _sessionActive = false;
                _pendingSubmit = false;
                HideOverlayIfNotAlwaysOn();
                ResetTimer();
            }
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_sessionActive && !_pendingSubmit)
            {
                ResetTimer();
                return;
            }

            EndSession("Manual end");
        }

        private void FinishCurrentRoomTiming()
        {
            var now = DateTime.Now;

            switch (_currentRoom)
            {
                case 1:
                    if (_room1Start.HasValue)
                    {
                        _room1Time += now - _room1Start.Value;
                        _room1Start = null;
                    }
                    break;

                case 2:
                    if (_room2Start.HasValue)
                    {
                        _room2Time += now - _room2Start.Value;
                        _room2Start = null;
                    }
                    break;

                case 3:
                    if (_room3Start.HasValue)
                    {
                        _room3Time += now - _room3Start.Value;
                        _room3Start = null;
                    }
                    break;
            }
        }

        private void EndSession(string reason)
        {
            StopCombatLogMonitor();
            if (_pendingSubmit)
            {
                SaveFinishedRun(reason);
                return;
            }

            if (!_sessionActive) return;

            _timer.Stop();
            FinishCurrentRoomTiming();

            var now = DateTime.Now;
            var elapsed = now - _sessionStart;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            double? lootMillions = ReadLootMillions();

            var record = new SessionRecord
            {
                Timestamp = now,
                Tier = _selectedTier,
                Weather = _selectedWeather,
                Room = _currentRoom,
                TotalRooms = 3,
                Elapsed = elapsed.ToString(@"mm\:ss"),
                Remaining = _remaining.ToString(@"mm\:ss"),
                Room1Time = _room1Time == TimeSpan.Zero ? string.Empty : _room1Time.ToString(@"mm\:ss"),
                Room2Time = _room2Time == TimeSpan.Zero ? string.Empty : _room2Time.ToString(@"mm\:ss"),
                Room3Time = _room3Time == TimeSpan.Zero ? string.Empty : _room3Time.ToString(@"mm\:ss"),
                LootMillions = lootMillions
            };

            SaveSessionToHistory(record);
            UpdateLastRunSummary(record);
            LoadHistoryIntoUi();
            RecalcStats();

            _sessionActive = false;
            HideOverlayIfNotAlwaysOn();
            ResetTimer();

            // Testing QoL: clear loot inputs after end/save
            _overlay?.ClearInvStartEnd();
            ClearMainLootFields();
        }

        private double? ReadLootMillions()
        {
            var nf = System.Globalization.NumberStyles.Float;
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            // Read from session state — single source of truth
            string before = _sessionLootBefore ?? "";
            string after  = _sessionLootAfter  ?? "";

            // Fall back to overlay fields if session state empty
            if (string.IsNullOrWhiteSpace(before)) before = _overlay?.GetInvStart() ?? "";
            if (string.IsNullOrWhiteSpace(after))  after  = _overlay?.GetInvEnd()   ?? "";

            if (!string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(after))
            {
                if (double.TryParse(before, nf, ci, out var b) &&
                    double.TryParse(after,  nf, ci, out var a))
                    return a - b;
            }
            return null;
        }

        private void ClearMainLootFields()
        {
            _sessionLootBefore = null;
            _sessionLootAfter  = null;
            if (MainInvStartTextBox != null) MainInvStartTextBox.Text = string.Empty;
            if (MainInvEndTextBox   != null) MainInvEndTextBox.Text   = string.Empty;
            if (MainLootDeltaText   != null) MainLootDeltaText.Text   = string.Empty;
        }

        // ============== Room Done buttons ===================
        private void Room1Done_Click(object sender, RoutedEventArgs e) => MarkRoomDone(1);
        private void Room2Done_Click(object sender, RoutedEventArgs e) => MarkRoomDone(2);
        private void Room3Done_Click(object sender, RoutedEventArgs e) => MarkRoomDone(3);

        private void MarkRoomDone(int roomDone)
        {
            if (!_sessionActive || roomDone < 1 || roomDone > 3 || _currentRoom != roomDone) return;

            FinishCurrentRoomTiming();

            if (roomDone < 3)
            {
                _currentRoom = roomDone + 1;
                var now = DateTime.Now;
                _currentRoomStart = now;

                if (_currentRoom == 2) _room2Start = now;
                if (_currentRoom == 3) _room3Start = now;

                // Flash drone reminder between rooms — only if player hasn't already flagged damage
                if (!_dronesNeedRepair)
                    _overlay?.FlashDroneReminder();

                // Clear detected NPCs — room is done, those NPCs are dead
                _detectedNames.Clear();
                ApplyNpcFilterAndRefresh();
                UpdateOverlayNpcs();

                UpdateRoomLabel();
                UpdateRunStateText();
                UpdateRoomButtonsVisual();
                UpdateOverlay();
                return;
            }

            // finished: now pending submit
            StopCombatLogMonitor();
            _timer.Stop();
            _sessionActive = false;
            _pendingSubmit = true;

            _finishTimestamp = DateTime.Now;
            _finishElapsed = _finishTimestamp - _sessionStart;
            if (_finishElapsed < TimeSpan.Zero) _finishElapsed = TimeSpan.Zero;

            _finishRemaining = _remaining;

            UpdateTimerDisplay();
            UpdateRoomLabel();
            UpdateRunStateText();
            UpdateRoomButtonsVisual();
            UpdateSubmitButtonVisual();
            UpdateOverlay();
        }

        // ============== Submit button (only when pending) =================
        private void LootSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_pendingSubmit)
            {
                MessageBox.Show(
                    "Finish the run first (click R3 Done), then Submit & End.\n\nOr press End to save immediately.",
                    "Not ready to submit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SaveFinishedRun("Submit");
        }

        private void SaveFinishedRun(string reason)
        {
            double? lootMillions = ReadLootMillions();

            var record = new SessionRecord
            {
                Timestamp = _finishTimestamp == DateTime.MinValue ? DateTime.Now : _finishTimestamp,
                Tier = _selectedTier,
                Weather = _selectedWeather,
                Room = 3,
                TotalRooms = 3,
                Elapsed = _finishElapsed.ToString(@"mm\:ss"),
                Remaining = _finishRemaining.ToString(@"mm\:ss"),
                Room1Time = _room1Time == TimeSpan.Zero ? string.Empty : _room1Time.ToString(@"mm\:ss"),
                Room2Time = _room2Time == TimeSpan.Zero ? string.Empty : _room2Time.ToString(@"mm\:ss"),
                Room3Time = _room3Time == TimeSpan.Zero ? string.Empty : _room3Time.ToString(@"mm\:ss"),
                LootMillions = lootMillions
            };

            SaveSessionToHistory(record);
            UpdateLastRunSummary(record);
            LoadHistoryIntoUi();
            RecalcStats();

            _pendingSubmit = false;
            HideOverlayIfNotAlwaysOn();
            ResetTimer();

            // Testing QoL: clear loot inputs after a successful submit
            _overlay?.ClearInvStartEnd();
            ClearMainLootFields();
        }

        // ========== Tier & Weather selection ==========
        private void TierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sessionActive || _pendingSubmit) return;

            if (sender is Button btn && btn.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                _selectedTier = tag;
                UpdateSelectionHighlights();
                UpdateOverlay();
            }
        }

        private void WeatherButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sessionActive || _pendingSubmit) return;

            if (sender is Button btn && btn.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                _selectedWeather = tag;
                UpdateSelectionHighlights();
                UpdateOverlay();
            }
        }

        private void SetTierFromOverlay(string? tierTag)
        {
            if (_sessionActive || _pendingSubmit) return;
            _selectedTier = tierTag ?? string.Empty;
            UpdateSelectionHighlights();
            UpdateOverlay();
        }

        private void SetWeatherFromOverlay(string? weatherTag)
        {
            if (_sessionActive || _pendingSubmit) return;
            _selectedWeather = weatherTag ?? string.Empty;
            UpdateSelectionHighlights();
            UpdateOverlay();
        }

        private void ToggleDroneArmorFlag()
        {
            _dronesNeedRepair = true;
            UpdateOverlay();
        }

        private void ClearDroneArmorFlag()
        {
            _dronesNeedRepair = false;
            UpdateOverlay();
        }

        // ============== History (UI) =================
        private void HistoryRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetHistoryStatus("Updated.");
            LoadHistoryIntoUi();
            RecalcStats();
        }

        private void HistoryClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all saved run history?\n\nThis will delete session_history.json and cannot be undone.",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (File.Exists(HistoryFilePath)) File.Delete(HistoryFilePath);
                SetHistoryStatus("History cleared.");
            }
            catch (Exception ex)
            {
                SetHistoryStatus($"Failed to clear history: {ex.Message}");
            }

            LoadHistoryIntoUi();
            RecalcStats();
        }

        private void LoadHistoryIntoUi()
        {
            try
            {
                var list = LoadHistory();
                var fullVm = list
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => new HistoryRow(r))
                    .ToList();

                if (HistoryListView != null) HistoryListView.ItemsSource = fullVm;
                if (HistoryCountTextBlock != null) HistoryCountTextBlock.Text = $"Runs: {fullVm.Count}";

                var previewVm = fullVm.Take(10).ToList();
                if (HistoryPreviewListView != null) HistoryPreviewListView.ItemsSource = previewVm;
                if (HistoryPreviewCountTextBlock != null) HistoryPreviewCountTextBlock.Text = $"(last {previewVm.Count})";
            }
            catch
            {
                if (HistoryCountTextBlock != null) HistoryCountTextBlock.Text = "Runs: 0";
                if (HistoryPreviewCountTextBlock != null) HistoryPreviewCountTextBlock.Text = "(last 0)";
            }
        }

        private List<SessionRecord> LoadHistory()
        {
            if (!File.Exists(HistoryFilePath)) return new List<SessionRecord>();

            try
            {
                var json = File.ReadAllText(HistoryFilePath);
                return JsonSerializer.Deserialize<List<SessionRecord>>(json) ?? new List<SessionRecord>();
            }
            catch
            {
                return new List<SessionRecord>();
            }
        }

        private void SaveSessionToHistory(SessionRecord record)
        {
            try
            {
                var history = LoadHistory();
                history.Add(record);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(history, options));
            }
            catch
            {
                // ignore
            }
        }

        private void UpdateLastRunSummary(SessionRecord r)
        {
            if (LastRunTextBlock == null) return;

            string timePart = r.Timestamp.ToString("G");
            string tier = string.IsNullOrEmpty(r.Tier) ? "Unknown tier" : r.Tier;
            string weather = string.IsNullOrEmpty(r.Weather) ? "Unknown weather" : r.Weather;
            string roomInfo = $"Room {r.Room}/{r.TotalRooms}";
            string elapsed = string.IsNullOrEmpty(r.Elapsed) ? "??:??" : r.Elapsed;

            string r1 = string.IsNullOrEmpty(r.Room1Time) ? "--:--" : r.Room1Time;
            string r2 = string.IsNullOrEmpty(r.Room2Time) ? "--:--" : r.Room2Time;
            string r3 = string.IsNullOrEmpty(r.Room3Time) ? "--:--" : r.Room3Time;

            string loot = r.LootMillions.HasValue ? $"{r.LootMillions.Value:0.##}M" : "n/a";

            LastRunTextBlock.Text =
                $"Last run: {timePart} – {tier} {weather} – {roomInfo} – {elapsed} (R1 {r1}, R2 {r2}, R3 {r3}) – Loot: {loot}";
        }

        // ============== Stats =================
        private void StatsRefreshButton_Click(object sender, RoutedEventArgs e) => RecalcStats();

        private void RecalcStats()
        {
            try
            {
                var history = LoadHistory();

                int totalRuns = history.Count;
                TimeSpan totalTime = TimeSpan.Zero;
                TimeSpan? bestRemaining = null;

                double totalLoot = 0;
                int lootCount = 0;

                foreach (var r in history)
                {
                    if (TryParseMmSs(r.Elapsed, out var t))
                        totalTime += t;

                    // Best run = most time REMAINING (most efficient clear)
                    if (TryParseMmSs(r.Remaining, out var rem))
                    {
                        if (bestRemaining == null || rem > bestRemaining.Value)
                            bestRemaining = rem;
                    }

                    if (r.LootMillions.HasValue)
                    {
                        totalLoot += r.LootMillions.Value;
                        lootCount++;
                    }
                }

                StatsTotalRunsText.Text = totalRuns.ToString();
                StatsTotalTimeText.Text = totalTime.ToString(@"hh\:mm\:ss");
                StatsAvgRunText.Text = (totalRuns > 0 && totalTime.TotalSeconds > 0)
                    ? TimeSpan.FromSeconds(totalTime.TotalSeconds / totalRuns).ToString(@"mm\:ss")
                    : "--:--";

                StatsBestRunText.Text = bestRemaining.HasValue
                    ? $"{bestRemaining.Value.ToString(@"mm\:ss")} left"
                    : "--:--";

                StatsTotalLootText.Text = totalLoot.ToString("0.##", CultureInfo.InvariantCulture);

                StatsAvgLootText.Text = (lootCount > 0)
                    ? (totalLoot / lootCount).ToString("0.##", CultureInfo.InvariantCulture)
                    : "--";

                // ISK/hour calculation — add 1 min re-entry wait between each run
                if (StatsIskHourText != null)
                {
                    if (lootCount > 0 && totalTime.TotalHours > 0)
                    {
                        // Each run has a ~60s mandatory re-entry cooldown (except the last run)
                        var reentryWait = TimeSpan.FromSeconds(Math.Max(0, totalRuns - 1) * 60);
                        var adjustedTime = totalTime + reentryWait;
                        double iskPerHour = totalLoot / adjustedTime.TotalHours;
                        StatsIskHourText.Text = $"{iskPerHour:0.##}M";
                    }
                    else
                    {
                        StatsIskHourText.Text = "--";
                    }
                }

                var byTier = history
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Tier) ? "Unknown" : r.Tier)
                    .OrderBy(g => g.Key)
                    .Select(g => new CountRow { Key = g.Key, Count = g.Count() })
                    .ToList();
                StatsByTierListView.ItemsSource = byTier;

                var byWeather = history
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Weather) ? "Unknown" : r.Weather)
                    .OrderBy(g => g.Key)
                    .Select(g => new CountRow { Key = g.Key, Count = g.Count() })
                    .ToList();
                StatsByWeatherListView.ItemsSource = byWeather;

                var byCombo = history
                    .GroupBy(r => new
                    {
                        Tier = string.IsNullOrWhiteSpace(r.Tier) ? "Unknown" : r.Tier,
                        Weather = string.IsNullOrWhiteSpace(r.Weather) ? "Unknown" : r.Weather
                    })
                    .OrderBy(g => g.Key.Tier)
                    .ThenBy(g => g.Key.Weather)
                    .Select(g => new TierWeatherRow { Tier = g.Key.Tier, Weather = g.Key.Weather, Count = g.Count() })
                    .ToList();
                StatsByTierWeatherListView.ItemsSource = byCombo;
            }
            catch
            {
                // ignore
            }
        }

        private static bool TryParseMmSs(string? s, out TimeSpan t)
        {
            t = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return TimeSpan.TryParseExact(s, @"mm\:ss", CultureInfo.InvariantCulture, out t);
        }

        // ============== NPC Library =================
        private void LoadNpcLibrary()
        {
            // Keep it stable: built-in defaults (you can expand later)
            _npcAll = NpcData.Build(_config);
            _npcFiltered = _npcAll.ToList();

            var families = _npcAll
                .Select(n => n.Family)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f)
                .ToList();

            families.Insert(0, "All");

            NpcGroupComboBox.ItemsSource = families;
            NpcGroupComboBox.SelectedIndex = 0;

            ApplyNpcFilterAndRefresh();
            SetNpcStatus(string.Empty);
        }

        private void NpcGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearDetectedMode();
            ApplyNpcFilterAndRefresh();
        }

        private void NpcFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ClearDetectedMode();
            ApplyNpcFilterAndRefresh();
        }

        private void ApplyNpcFilterAndRefresh()
        {
            string selectedFamily = NpcGroupComboBox?.SelectedItem as string ?? "All";
            string search = NpcFilterTextBox?.Text ?? string.Empty;

            IEnumerable<NpcEntry> query = _npcAll;

            if (!string.IsNullOrWhiteSpace(selectedFamily) && selectedFamily != "All")
                query = query.Where(n => string.Equals(n.Family, selectedFamily, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(n =>
                    n.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    n.Family.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    n.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    n.Handle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (n.Tags != null && n.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)))
                );
            }

            // Detected view:
            // - When detected mode is ON and there are 0 names, we show an empty list.
            //   Only "Show All" repopulates the full list.
            if (_detectedMode)
            {
                if (_detectedNames.Count == 0)
                {
                    _npcFiltered = new List<NpcEntry>();
                    NpcListView.ItemsSource = _npcFiltered;
                    NpcMatchesTextBlock.Text = "0 matches";
                    return;
                }

                query = query.Where(n => _detectedNames.Contains(n.Name));
            }

            _npcFiltered = query.OrderBy(n => n.Family).ThenBy(n => n.Name).ToList();
            NpcListView.ItemsSource = _npcFiltered;
            NpcMatchesTextBlock.Text = $"{_npcFiltered.Count} matches";
        }

        private void NpcListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NpcListView?.SelectedItem is not NpcEntry npc)
            {
                NpcDetailName.Text = "(none)";
                NpcDetailLine.Text = "";
                NpcDetailNotes.Text = "Click an NPC on the right.";
                NpcDetailHandle.Text = "Select an NPC on the right.";
                NpcDetailTags.ItemsSource = null;
                if (NpcDatasetText != null) NpcDatasetText.Text = string.Empty;
                return;
            }

            NpcDetailName.Text = npc.Name;
            NpcDetailLine.Text = $"{npc.Family} • {npc.Class} • Threat: {npc.Threat} • Kill: {npc.KillPriority}";
            NpcDetailNotes.Text = string.IsNullOrWhiteSpace(npc.Notes) ? "(no notes)" : npc.Notes;
            NpcDetailHandle.Text = string.IsNullOrWhiteSpace(npc.Handle) ? BuildNpcHandleText(npc) : npc.Handle;
            NpcDetailTags.ItemsSource = (npc.Tags != null && npc.Tags.Count > 0) ? npc.Tags : new List<string> { "(none)" };
            if (NpcDatasetText != null) NpcDatasetText.Text = BuildNpcDatasetText(npc);
        }

        
private static string BuildNpcDatasetText(NpcEntry npc)
{
    // Uses optional extended fields populated from npc_dataset.json (if enabled).
    if (npc == null) return string.Empty;

    bool hasAny =
        !string.IsNullOrWhiteSpace(npc.NpcFamily) ||
        !string.IsNullOrWhiteSpace(npc.Ewar) ||
        !string.IsNullOrWhiteSpace(npc.DamageDealt) ||
        !string.IsNullOrWhiteSpace(npc.WeakTo) ||
        !string.IsNullOrWhiteSpace(npc.Behaviors) ||
        !string.IsNullOrWhiteSpace(npc.StatsNotes) ||
        npc.ShieldHp.HasValue || npc.ArmorHp.HasValue || npc.HullHp.HasValue || npc.Dps.HasValue;

    if (!hasAny)
        return "(No abyss dataset info for this NPC.)";

    var sb = new System.Text.StringBuilder();

    if (!string.IsNullOrWhiteSpace(npc.NpcFamily))
        sb.AppendLine($"Faction: {npc.NpcFamily}");

    sb.AppendLine($"Class: {npc.Class}");

    if (!string.IsNullOrWhiteSpace(npc.Ewar))
        sb.AppendLine($"EWAR: {npc.Ewar}");

    if (!string.IsNullOrWhiteSpace(npc.DamageDealt))
        sb.AppendLine($"Damage: {npc.DamageDealt}");

    if (!string.IsNullOrWhiteSpace(npc.WeakTo))
        sb.AppendLine($"Weak to: {npc.WeakTo}");

    // Stats (if present)
    if (npc.ShieldHp.HasValue || npc.ArmorHp.HasValue || npc.HullHp.HasValue || npc.Dps.HasValue)
    {
        sb.AppendLine("");
        sb.AppendLine("Approx stats:");
        if (npc.ShieldHp.HasValue) sb.AppendLine($"  Shield HP: {npc.ShieldHp.Value}");
        if (npc.ArmorHp.HasValue) sb.AppendLine($"  Armor HP:  {npc.ArmorHp.Value}");
        if (npc.HullHp.HasValue) sb.AppendLine($"  Hull HP:   {npc.HullHp.Value}");
        if (npc.Dps.HasValue) sb.AppendLine($"  DPS:       {npc.Dps.Value:0.##}");
    }

    if (!string.IsNullOrWhiteSpace(npc.StatsNotes))
    {
        sb.AppendLine("");
        sb.AppendLine($"Notes: {npc.StatsNotes}");
    }

    if (!string.IsNullOrWhiteSpace(npc.Behaviors))
    {
        sb.AppendLine("");
        sb.AppendLine($"Behavior: {npc.Behaviors}");
    }

    return sb.ToString().TrimEnd();
}

private static string BuildNpcHandleText(NpcEntry npc)
        {
            string kill = string.IsNullOrWhiteSpace(npc.KillPriority) ? "Use judgment" : npc.KillPriority;
            string notes = string.IsNullOrWhiteSpace(npc.Notes) ? "" : npc.Notes;
            return $"Kill: {kill}\n{notes}";
        }

        private void NpcShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            ClearDetectedMode();
            NpcFilterTextBox.Text = string.Empty;
            NpcGroupComboBox.SelectedIndex = 0;
            SetNpcStatus(string.Empty);
            ApplyNpcFilterAndRefresh();
        }

        private void NpcShowDetectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_detectedMode || _detectedNames.Count == 0)
            {
                SetNpcStatus("Detected view is empty — no NPCs detected from combat log yet.");
                return;
            }

            ApplyNpcFilterAndRefresh();
            SetNpcStatus($"Detected view active: {_detectedNames.Count} name(s).");
        }

        private void NpcClearDetectedButton_Click(object sender, RoutedEventArgs e)
        {
            // User intent: keep Detected view active, clear list, and keep watching the logs.
            _detectedMode = true;
            _detectedNames.Clear();
            NpcGroupComboBox.SelectedIndex = 0;
            ApplyNpcFilterAndRefresh();
            StartCombatLogMonitorIfEnabled();
            SetNpcStatus("Detected cleared (watching combat log).");
        }

        private void NpcClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            NpcFilterTextBox.Text = string.Empty;
            ClearDetectedMode();
            ApplyNpcFilterAndRefresh();
        }

        private void ClearDetectedMode()
        {
            if (!_detectedMode) return;
            _detectedMode = false;
            _detectedNames.Clear();
        }

        private void SetNpcStatus(string msg)
        {
            NpcOverviewStatusTextBlock.Text = msg ?? "";
        }

        // ------------------- NPC dataset (optional) -------------------

        private void MainInvStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            _sessionLootBefore = MainInvStartTextBox.Text;
            _overlay?.SyncInvStart(MainInvStartTextBox.Text);
            UpdateLootDelta();
        }

        private void MainInvEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            _sessionLootAfter = MainInvEndTextBox.Text;
            _overlay?.SyncInvEnd(MainInvEndTextBox.Text);
            UpdateLootDelta();
        }

        private void UpdateLootDelta()
        {
            if (MainLootDeltaText == null) return;
            var nf = System.Globalization.NumberStyles.Float;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string before = _sessionLootBefore ?? "";
            string after  = _sessionLootAfter  ?? "";
            if (double.TryParse(before, nf, ci, out var b) && double.TryParse(after, nf, ci, out var a))
                MainLootDeltaText.Text = $"= {(a - b):+0.##;-0.##} M";
            else
                MainLootDeltaText.Text = "";
        }

        private void CockpitModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.CockpitMode = CockpitModeCheckBox.IsChecked == true;
            _config.Save();
            ApplyCockpitMode(_config.CockpitMode);
        }

        private void ApplyCockpitMode(bool cockpit)
        {
            if (CockpitBar != null)
                CockpitBar.Visibility = cockpit ? Visibility.Visible : Visibility.Collapsed;
            if (StandardRunControls != null)
                StandardRunControls.Visibility = cockpit ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RunSetupButton_Click(object sender, RoutedEventArgs e)
        {
            var setup = new SetupWindow(_config);
            if (setup.ShowDialog() == true)
            {
                ApplyCockpitMode(_config.CockpitMode);
                CockpitModeCheckBox.IsChecked = _config.CockpitMode;
                EnableCombatLogCheckBox.IsChecked = _config.EnableCombatLogMonitor;
                CombatLogPathText.Text = _config.CombatLogFolder;
            }
        }

        private void LaunchOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            ShowOverlay();
        }

        private void ToggleOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay != null && _overlay.IsVisible)
            {
                _overlay.Hide();
                if (LaunchOverlayStandardButton != null)
                    LaunchOverlayStandardButton.Content = "Overlay ▶";
            }
            else
            {
                ShowOverlay();
                if (LaunchOverlayStandardButton != null)
                    LaunchOverlayStandardButton.Content = "Overlay ■";
            }
        }

        private void ReloadNpcDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadNpcDataset();
        }

        private void ReloadNpcDataset()
        {
            LoadNpcLibrary();
            var status = NpcData.LastLoadStatus;
            if (string.IsNullOrWhiteSpace(status))
            {
                SetNpcStatus($"NPCs loaded: {_npcAll.Count}");
            }
            else
            {
                SetNpcStatus($"NPCs loaded: {_npcAll.Count} | {status}");
            }
        }

        // ------------------- Combat log monitor (optional) -------------------

        private void EnableCombatLogCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.EnableCombatLogMonitor = EnableCombatLogCheckBox.IsChecked == true;
            _config.Save();

            if (!_config.EnableCombatLogMonitor)
            {
                StopCombatLogMonitor();
            }
            else
            {
                // If a session is already running, start immediately.
                StartCombatLogMonitorIfEnabled();
            }
        }

        private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = _config.CombatLogFolder;
                if (Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"Log folder not found:\n{folder}", "Combat log", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder.\n\n{ex.Message}", "Combat log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartCombatLogMonitorIfEnabled()
        {
            if (!_sessionActive) return;
            if (EnableCombatLogCheckBox.IsChecked != true) return;

            if (_combatLogMonitor != null)
            {
                // Already running — reset _seen so NPCs from previous run don't block detection
                _combatLogMonitor.Start(ignoreExistingLines: true);
                return;
            }

            _combatLogMonitor = new CombatLogMonitor(_config.CombatLogFolder);
            _combatLogMonitor.NpcSeen += CombatLogMonitor_NpcSeen;
            _combatLogMonitor.StartMonitoring(ignoreExistingLines: true);

            _combatLogTimer.Start();
            SetNpcStatus("Combat log: monitoring (auto-detect enabled).");
        }

        private void StopCombatLogMonitor()
        {
            try
            {
                _combatLogTimer.Stop();
            }
            catch { /* ignore */ }

            if (_combatLogMonitor != null)
            {
                try
                {
                    _combatLogMonitor.NpcSeen -= CombatLogMonitor_NpcSeen;
                    _combatLogMonitor.StopMonitoring();
                    _combatLogMonitor.Dispose();
                }
                catch { /* ignore */ }
                finally
                {
                    _combatLogMonitor = null;
                }
            }
        }

        private void CombatLogTimer_Tick(object? sender, EventArgs e)
        {
            if (_combatLogMonitor == null) return;
            if (!_sessionActive) return;

            try
            {
                _combatLogMonitor.Poll();
            }
            catch
            {
                // If anything goes weird, stop safely (no crashing during a run).
                StopCombatLogMonitor();
                SetNpcStatus("Combat log: stopped (error while reading).");
            }
        }

        private void AutoClearDetectedTimer_Tick(object? sender, EventArgs e)
        {
            // Never clear during an active run or pending submit
            if (_sessionActive || _pendingSubmit) return;
            if (!_detectedMode) return;

            // Only clear when not in a run — between runs cleanup
            if (_detectedNames.Count > 0)
            {
                _detectedNames.Clear();
                ApplyNpcFilterAndRefresh();
                UpdateOverlayNpcs();
            }
        }

        private void CombatLogMonitor_NpcSeen(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;

            var resolvedName = NpcNameResolver.ResolveToLibraryName(npcName, _npcAll);
            if (string.IsNullOrWhiteSpace(resolvedName)) return;

            // Ensure NPC exists in list
            EnsureNpcExists(resolvedName);

            // Switch to detected mode
            if (!_detectedMode)
            {
                _detectedMode = true;
                _detectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                NpcGroupComboBox.SelectedIndex = 0;
            }

            if (_detectedNames.Add(resolvedName))
            {
                ApplyNpcFilterAndRefresh();
                UpdateOverlayNpcs();
            }
        }

        private void EnsureNpcExists(string npcName)
        {
            if (_npcAll.Any(n => string.Equals(n.Name, npcName, StringComparison.OrdinalIgnoreCase)))
                return;

            var entry = InferNpcFromName(npcName.Trim());
            _npcAll.Add(entry);

            // Rebuild families list
            var families = _npcAll
                .Select(n => n.Family)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f)
                .ToList();

            families.Insert(0, "All");
            NpcGroupComboBox.ItemsSource = families;
        }

        /// <summary>
        /// Infers NPC details from the name alone using EVE naming conventions.
        /// No API needed — works purely from pattern matching.
        /// </summary>
        private static NpcEntry InferNpcFromName(string name)
        {
            var entry = new NpcEntry
            {
                Name = name,
                Notes = "Auto-detected from combat log — verify details in NPC Library.",
                Tags = new List<string> { "Auto-detected" }
            };

            var n = name.ToUpperInvariant();

            // ===== TRIGLAVIAN HULLS =====
            if (n.Contains("DAMAVIK"))       { entry.Family = "Damavik";     entry.Class = "Frigate"; }
            else if (n.Contains("KIKIMORA")) { entry.Family = "Kikimora";    entry.Class = "Destroyer"; }
            else if (n.Contains("VEDMAK"))   { entry.Family = "Vedmak";      entry.Class = "Cruiser"; }
            else if (n.Contains("RODIVA"))   { entry.Family = "Rodiva";      entry.Class = "Cruiser"; }
            else if (n.Contains("DREKAVAC")) { entry.Family = "Drekavac";    entry.Class = "Battlecruiser"; }
            else if (n.Contains("LESHAK"))   { entry.Family = "Leshak";      entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }

            // ===== DRIFTERS/SEEKERS =====
            else if (n.Contains("TYRANNOS") || n.Contains("DRIFTER")) { entry.Family = "Drifter"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }
            else if (n.Contains("SEEKER"))   { entry.Family = "Seeker"; entry.Class = "Frigate"; entry.Threat = "High"; entry.KillPriority = "Early"; }

            // ===== SLEEPERS =====
            else if (n.Contains("EPHIALTES") || n.Contains("LUCID UPHOLDER") || n.Contains("LUCID WARDEN")) { entry.Family = "Sleeper"; entry.Threat = "High"; entry.KillPriority = "Early"; entry.Tags.Add("Sleeper"); }

            // ===== EDENCOM/CONCORD =====
            else if (n.Contains("SKYBREAKER"))  { entry.Family = "EDENCOM"; entry.Class = "Frigate";    entry.Threat = "High";      entry.KillPriority = "Early"; entry.Notes = "Hard to kill — good active shield tank. " + entry.Notes; }
            else if (n.Contains("STORMBRINGER")){ entry.Family = "EDENCOM"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }
            else if (n.Contains("THUNDERCHILD")){ entry.Family = "EDENCOM"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }
            else if (n.Contains("PACIFIER") || n.Contains("DISPARU")) { entry.Family = "EDENCOM"; entry.Class = "Frigate"; entry.Threat = "High"; entry.KillPriority = "Early"; }
            else if (n.Contains("ENFORCER") && !n.Contains("ABYSS")) { entry.Family = "EDENCOM"; entry.Class = "Cruiser"; entry.Threat = "High"; entry.KillPriority = "Early"; }
            else if (n.Contains("MARSHAL"))     { entry.Family = "EDENCOM"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }

            // ===== ROGUE DRONES =====
            else if (n.Contains("TESSELLA") || n.Contains("TESSERA")) { entry.Family = "Rogue Drone"; entry.Class = "Frigate"; }
            else if (n.Contains("OVERMIND"))    { entry.Family = "Rogue Drone"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; entry.Notes = "Very high HP — timeout killer. " + entry.Notes; }

            // ===== ANGELS (LUCIFER) =====
            else if (n.Contains("DRAMIEL"))     { entry.Family = "Angels (Lucifer)"; entry.Class = "Frigate";    entry.Threat = "High"; entry.KillPriority = "Early"; }
            else if (n.Contains("CYNABAL"))     { entry.Family = "Angels (Lucifer)"; entry.Class = "Cruiser";    entry.Threat = "High"; entry.KillPriority = "Early"; entry.Ewar = "Web/Neut"; }
            else if (n.Contains("MACHARIEL"))   { entry.Family = "Angels (Lucifer)"; entry.Class = "Battleship"; entry.Threat = "Very High"; entry.KillPriority = "First"; }
            else if (n.Contains("LUCIFER"))     { entry.Family = "Angels (Lucifer)"; entry.Threat = "High"; entry.KillPriority = "Early"; }

            // ===== SANSHA =====
            else if (n.Contains("DEVOTED"))     { entry.Family = "Sansha (Devoted)"; entry.Threat = "High"; entry.KillPriority = "Early"; }

            // ===== FALLBACK =====
            else { entry.Family = "Unknown"; }

            // ===== TRIGLAVIAN PREFIX INFERENCE (threat/ewar from prefix) =====
            if (string.IsNullOrWhiteSpace(entry.Threat))
            {
                if      (n.StartsWith("STARVING") || n.StartsWith("DEVOURING")) { entry.Threat = "Very High"; entry.KillPriority = "First"; entry.Ewar = "Neut"; entry.Tags.Add("Neut"); }
                else if (n.StartsWith("ANCHORING") || n.StartsWith("TANGLING")) { entry.Threat = "High"; entry.KillPriority = "Early"; entry.Ewar = "Tackle"; entry.Tags.Add("Tackle"); }
                else if (n.StartsWith("BLINDING"))                               { entry.Threat = "High"; entry.KillPriority = "Early"; entry.Ewar = "Damp"; entry.Tags.Add("Damp"); }
                else if (n.StartsWith("GHOSTING"))                               { entry.Threat = "High"; entry.KillPriority = "Early"; entry.Ewar = "TrackingDisrupt"; entry.Tags.Add("TrackingDisrupt"); }
                else if (n.StartsWith("HARROWING") || n.StartsWith("STRIKING")) { entry.Threat = "High"; entry.KillPriority = "Early"; }
                else if (n.StartsWith("LUCID") || n.StartsWith("RENEWING"))     { entry.Threat = "High"; entry.KillPriority = "Early"; }
                else { entry.Threat = "High"; entry.KillPriority = "Mid"; }
            }

            // ===== ROGUE DRONE EWAR FROM PREFIX =====
            if (entry.Family == "Rogue Drone" && string.IsNullOrWhiteSpace(entry.Ewar))
            {
                if      (n.Contains("SNARE")) { entry.Ewar = "Web"; entry.Tags.Add("Web"); }
                else if (n.Contains("FOG"))   { entry.Ewar = "TrackingDisrupt"; entry.Tags.Add("Tracking"); }
                else if (n.Contains("GAZE"))  { entry.Ewar = "Damp"; entry.Tags.Add("Damp"); }
                else if (n.Contains("SPOT"))  { entry.Ewar = "Paint"; entry.Tags.Add("Paint"); }
            }

            // ===== VILA VARIANT =====
            if (n.Contains("VILA"))
            {
                entry.Threat = "Very High";
                entry.Notes = "Vila variant — treat as higher threat than standard. " + entry.Notes;
                if (!entry.Tags.Contains("Vila")) entry.Tags.Add("Vila");
            }

            // Family tag
            if (!string.IsNullOrWhiteSpace(entry.Family) && entry.Family != "Unknown")
                entry.Tags.Insert(0, entry.Family);

            // Default handle
            if (string.IsNullOrWhiteSpace(entry.Handle))
                entry.Handle = $"Kill priority: {(string.IsNullOrWhiteSpace(entry.KillPriority) ? "Mid" : entry.KillPriority)}. Check NPC library for full details.";

            return entry;
        }

        // ========== Tier & Weather selection from UI ==========
        // (Buttons already wired)

        // ========== About tab links ==========
        private void KofiLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ko-fi.com/jakkelsza") { UseShellExecute = true });

        private void PaypalLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://paypal.me/JakkelsZA") { UseShellExecute = true });

        private void GithubLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/JakkelsZA") { UseShellExecute = true });
    }

    // ===================== Data Models =====================
    public class SessionRecord
    {
        public DateTime Timestamp { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Weather { get; set; } = string.Empty;
        public int Room { get; set; }
        public int TotalRooms { get; set; }
        public string Elapsed { get; set; } = string.Empty;
        public string Remaining { get; set; } = string.Empty;
        public string Room1Time { get; set; } = string.Empty;
        public string Room2Time { get; set; } = string.Empty;
        public string Room3Time { get; set; } = string.Empty;
        public double? LootMillions { get; set; }
    }

    public class HistoryRow
    {
        public string TimestampDisplay { get; }
        public string Tier { get; }
        public string Weather { get; }
        public string RoomsDisplay { get; }
        public string Elapsed { get; }
        public string Remaining { get; }
        public string SplitsDisplay { get; }
        public string LootDisplay { get; }

        public HistoryRow(SessionRecord r)
        {
            TimestampDisplay = r.Timestamp.ToString("G");
            Tier = r.Tier;
            Weather = r.Weather;
            RoomsDisplay = $"{r.Room}/{r.TotalRooms}";
            Elapsed = r.Elapsed;
            Remaining = r.Remaining;

            var r1 = string.IsNullOrWhiteSpace(r.Room1Time) ? "--:--" : r.Room1Time;
            var r2 = string.IsNullOrWhiteSpace(r.Room2Time) ? "--:--" : r.Room2Time;
            var r3 = string.IsNullOrWhiteSpace(r.Room3Time) ? "--:--" : r.Room3Time;
            SplitsDisplay = $"R1 {r1} R2 {r2} R3 {r3}";

            LootDisplay = r.LootMillions.HasValue
                ? r.LootMillions.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : "";
        }
    }

    public class CountRow
    {
        public string Key { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TierWeatherRow
    {
        public string Tier { get; set; } = string.Empty;
        public string Weather { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    // ============================================================
    // =================== MainWindow class END ====================
    // ============================================================
}