using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EveAbyssCompanion
{
    public partial class OverlayWindow : Window
    {
        // Win32 constants to prevent overlay stealing focus from EVE
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 3;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;

            // WS_EX_TOOLWINDOW: doesn't appear in Alt+Tab
            // Remove WS_EX_NOACTIVATE so textboxes can receive keyboard input
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);

            // Instead intercept WM_MOUSEACTIVATE to block activation on button clicks
            // but allow it for textboxes
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE)
            {
                // Check if the mouse is over a TextBox — if so allow activation
                var mousePos = Mouse.GetPosition(this);
                var hit = InputHitTest(mousePos);

                if (hit is TextBox || FindParent<TextBox>(hit as DependencyObject) != null)
                {
                    // Allow activation so textbox can receive keyboard input
                    return IntPtr.Zero;
                }

                // Block activation for everything else — EVE keeps focus
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent is T t) return t;
            return FindParent<T>(parent);
        }
        private readonly Action _onStartClicked;
        private readonly Action _onSubmitClicked;
        private readonly Action _onEndNowClicked;
        private readonly Action _onSetupClicked;
        private readonly Action<int> _onRoomDoneClicked;
        private readonly Action<string> _onTierSelected;
        private readonly Action<string> _onWeatherSelected;
        private readonly Action _onToggleDroneArmor;
        private readonly Action _onDroneRepaired;
        private readonly Action _onClearDetected;

        private bool _allowTierWeatherClicks = true;

        // Drone reminder flash timer
        private readonly DispatcherTimer _droneFlashTimer;
        private int _droneFlashCount;

        public OverlayWindow(
            Action onStartClicked,
            Action onSubmitClicked,
            Action onEndNowClicked,
            Action onSetupClicked,
            Action<int> onRoomDoneClicked,
            Action<string> onTierSelected,
            Action<string> onWeatherSelected,
            Action onToggleDroneArmor,
            Action? onDroneRepaired = null,
            Action? onClearDetected = null)
        {
            InitializeComponent();

            _onStartClicked    = onStartClicked;
            _onSubmitClicked   = onSubmitClicked;
            _onEndNowClicked   = onEndNowClicked;
            _onSetupClicked    = onSetupClicked;
            _onRoomDoneClicked = onRoomDoneClicked;
            _onTierSelected    = onTierSelected;
            _onWeatherSelected = onWeatherSelected;
            _onToggleDroneArmor = onToggleDroneArmor;
            _onDroneRepaired    = onDroneRepaired ?? (() => { });
            _onClearDetected    = onClearDetected ?? (() => { });

            _droneFlashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _droneFlashTimer.Tick += DroneFlashTimer_Tick;

            UpdateInvDeltaLabel();
        }

        // ===== Public API =====

        // Raised when the user edits loot fields directly in the overlay
        public event Action<string, string>? InvChanged;

        public string GetInvStart() => InvStartTextBox?.Text?.Trim() ?? "";
        public string GetInvEnd()   => InvEndTextBox?.Text?.Trim()   ?? "";

        public bool TryGetInvStartEnd(out double startM, out double endM)
        {
            startM = 0; endM = 0;
            if (InvStartTextBox == null || InvEndTextBox == null) return false;
            if (!double.TryParse(InvStartTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out startM)) return false;
            if (!double.TryParse(InvEndTextBox.Text,   NumberStyles.Float, CultureInfo.InvariantCulture, out endM))   return false;
            return true;
        }

        // Called by main window to push its field values into the overlay without looping
        private bool _syncingInv = false;
        public void SyncInvStart(string val)
        {
            if (_syncingInv || InvStartTextBox == null) return;
            if (InvStartTextBox.Text == val) return;
            _syncingInv = true;
            InvStartTextBox.Text = val;
            _syncingInv = false;
        }
        public void SyncInvEnd(string val)
        {
            if (_syncingInv || InvEndTextBox == null) return;
            if (InvEndTextBox.Text == val) return;
            _syncingInv = true;
            InvEndTextBox.Text = val;
            _syncingInv = false;
        }

        public void ClearInvStartEnd()
        {
            if (InvStartTextBox != null) InvStartTextBox.Text = string.Empty;
            if (InvEndTextBox   != null) InvEndTextBox.Text   = string.Empty;
            UpdateInvDeltaLabel();
        }

        /// <summary>Updates the detected NPC list shown at the bottom of the overlay.</summary>
        public void UpdateDetectedNpcs(IEnumerable<NpcEntry> npcs)
        {
            var list = npcs?.ToList() ?? new List<NpcEntry>();

            if (DetectedNpcList != null)
                DetectedNpcList.ItemsSource = list;

            if (DetectedNpcStatus != null)
                DetectedNpcStatus.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Flashes the drone reminder text briefly when transitioning rooms.</summary>
        public void FlashDroneReminder()
        {
            if (DroneReminderText == null) return;
            DroneReminderText.Text = "🛸 Check drone HP!";
            _droneFlashCount = 0;
            _droneFlashTimer.Start();
        }

        public void UpdateDisplay(
            TimeSpan remaining,
            int currentRoom,
            string selectedTier,
            string selectedWeather,
            bool isRunning,
            bool isPendingSubmit,
            TimeSpan currentRoomElapsed,
            bool dronesNeedRepair)
        {
            TimerText.Text = remaining.ToString(@"mm\:ss");
            RoomText.Text  = currentRoom <= 0 ? "Room: 0 / 3" : $"Room: {currentRoom} / 3";

            if (isPendingSubmit)       StatusText.Text = "FINISHED • Waiting for Submit";
            else if (isRunning)        StatusText.Text = $"RUNNING • Room {currentRoom}";
            else                       StatusText.Text = "READY";

            var tierPart    = string.IsNullOrWhiteSpace(selectedTier)    ? "Tier: (none)"    : selectedTier;
            var weatherPart = string.IsNullOrWhiteSpace(selectedWeather) ? "Weather: (none)" : selectedWeather;
            SelectionText.Text = $"{tierPart} • {weatherPart}";

            StartButton.IsEnabled  = !isRunning && !isPendingSubmit;
            SubmitButton.IsEnabled = isPendingSubmit;
            EndNowButton.IsEnabled = isRunning || isPendingSubmit;

            R1DoneButton.IsEnabled = isRunning && currentRoom == 1;
            R2DoneButton.IsEnabled = isRunning && currentRoom == 2;
            R3DoneButton.IsEnabled = isRunning && currentRoom == 3;

            _allowTierWeatherClicks = !isRunning && !isPendingSubmit;
            if (TierGrid    != null) TierGrid.IsHitTestVisible    = _allowTierWeatherClicks;
            if (WeatherGrid != null) WeatherGrid.IsHitTestVisible = _allowTierWeatherClicks;

            HighlightSelectedButton(TierGrid,    selectedTier);
            HighlightSelectedButton(WeatherGrid, selectedWeather);

            // Drone reminder — show Repaired button only when flag is set
            if (dronesNeedRepair)
            {
                if (!_droneFlashTimer.IsEnabled)
                    DroneReminderText.Text = "⚠ Repair drones!";
                if (DroneRepairedButton != null)
                    DroneRepairedButton.Visibility = Visibility.Visible;
            }
            else
            {
                if (!_droneFlashTimer.IsEnabled)
                    DroneReminderText.Text = string.Empty;
                if (DroneRepairedButton != null)
                    DroneRepairedButton.Visibility = Visibility.Collapsed;
            }

            RootBorder.Background = new SolidColorBrush(GetPressureColor(currentRoomElapsed, isRunning, isPendingSubmit));
            UpdateInvDeltaLabel();
        }

        // ===== Private helpers =====

        private void HighlightSelectedButton(Panel? panel, string selectedTag)
        {
            if (panel == null) return;
            foreach (var btn in panel.Children.OfType<Button>())
            {
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x42, 0x52));
                btn.FontWeight = FontWeights.Normal;
            }
            if (string.IsNullOrWhiteSpace(selectedTag)) return;
            foreach (var btn in panel.Children.OfType<Button>())
            {
                if (btn.Tag is string tag && string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase))
                {
                    btn.BorderThickness = new Thickness(2);
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0xA6, 0xFF));
                    btn.FontWeight = FontWeights.SemiBold;
                    break;
                }
            }
        }

        private static Color GetPressureColor(TimeSpan roomElapsed, bool isRunning, bool isPendingSubmit)
        {
            if (isPendingSubmit || !isRunning) return Color.FromArgb(0xBB, 0x0B, 0x0F, 0x14);
            if (roomElapsed.TotalMinutes < 5)  return Color.FromArgb(0xBB, 0x00, 0x28, 0x00);
            if (roomElapsed.TotalMinutes < 7)  return Color.FromArgb(0xBB, 0x38, 0x28, 0x00);
            return                                    Color.FromArgb(0xBB, 0x38, 0x00, 0x00);
        }

        private void DroneFlashTimer_Tick(object? sender, EventArgs e)
        {
            _droneFlashCount++;
            if (DroneReminderText == null) { _droneFlashTimer.Stop(); return; }

            // Flash 4 times then stop
            DroneReminderText.Visibility = _droneFlashCount % 2 == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_droneFlashCount >= 8)
            {
                _droneFlashTimer.Stop();
                DroneReminderText.Visibility = Visibility.Visible;
                DroneReminderText.Text = string.Empty;
            }
        }

        private void UpdateInvDeltaLabel()
        {
            if (InvDeltaText == null) return;
            if (TryGetInvStartEnd(out var startM, out var endM))
                InvDeltaText.Text = $"Δ: {(endM - startM):0.##}M";
            else
                InvDeltaText.Text = "Δ: --";
        }

        // ===== Event handlers =====
        private void OverlayDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Start_Click(object sender, RoutedEventArgs e)  => _onStartClicked?.Invoke();
        private void Submit_Click(object sender, RoutedEventArgs e) => _onSubmitClicked?.Invoke();
        private void EndNow_Click(object sender, RoutedEventArgs e) => _onEndNowClicked?.Invoke();
        private void SetupButton_Click(object sender, RoutedEventArgs e) => _onSetupClicked?.Invoke();

        private void R1Done_Click(object sender, RoutedEventArgs e) => _onRoomDoneClicked?.Invoke(1);
        private void R2Done_Click(object sender, RoutedEventArgs e) => _onRoomDoneClicked?.Invoke(2);
        private void R3Done_Click(object sender, RoutedEventArgs e) => _onRoomDoneClicked?.Invoke(3);

        private void TierButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_allowTierWeatherClicks) return;
            if (sender is Button btn && btn.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                _onTierSelected?.Invoke(tag);
        }

        private void WeatherButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_allowTierWeatherClicks) return;
            if (sender is Button btn && btn.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                _onWeatherSelected?.Invoke(tag);
        }

        private void DroneArmor_Click(object sender, RoutedEventArgs e) => _onToggleDroneArmor?.Invoke();
        private void DroneRepaired_Click(object sender, RoutedEventArgs e) => _onDroneRepaired?.Invoke();

        private void ClearDetected_Click(object sender, RoutedEventArgs e) => _onClearDetected?.Invoke();

        private void HideOverlay_Click(object sender, RoutedEventArgs e) => Hide();

        private void InvText_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateInvDeltaLabel();
            if (!_syncingInv)
                InvChanged?.Invoke(InvStartTextBox?.Text ?? "", InvEndTextBox?.Text ?? "");
        }
    }

    // Converts overlay_tag string to a SolidColorBrush for chip text colour
    public class OverlayTagToColourConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush Neut    = new(Color.FromRgb(0xFF, 0xD7, 0x00)); // Yellow
        private static readonly SolidColorBrush Scram   = new(Color.FromRgb(0xFF, 0x5A, 0x5A)); // Red
        private static readonly SolidColorBrush Web     = new(Color.FromRgb(0xFF, 0xA0, 0x40)); // Orange
        private static readonly SolidColorBrush Damp    = new(Color.FromRgb(0xCC, 0x88, 0xFF)); // Purple
        private static readonly SolidColorBrush Paint   = new(Color.FromRgb(0x99, 0x99, 0x99)); // Grey
        private static readonly SolidColorBrush Logi    = new(Color.FromRgb(0x4C, 0xFF, 0xB3)); // Green
        private static readonly SolidColorBrush Default = new(Color.FromRgb(0xF2, 0xF6, 0xFA)); // White

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Neut"    => Neut,
                "Scram"   => Scram,
                "Web"     => Web,
                "Damp"    => Damp,
                "Disrupt" => Damp,
                "Paint"   => Paint,
                "Logi"    => Logi,
                _         => Default
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Converts is_boss bool to gold border brush or transparent
    public class BossBorderConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush Gold        = new(Color.FromRgb(0xFF, 0xD7, 0x00));
        private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Gold : Transparent;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
