using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace EveAbyssCompanion
{
    public partial class SetupWindow : Window
    {
        private readonly AppConfig _config;
        private int _step = 1;
        private const int TotalSteps = 3;

        public SetupWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;

            // Pre-fill log folder with default or existing config
            var defaultPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "EVE", "logs", "Gamelogs");

            LogFolderBox.Text = string.IsNullOrWhiteSpace(_config.CombatLogFolder)
                ? defaultPath
                : _config.CombatLogFolder.Replace(@"\\", @"\");

            // Set initial mode radio
            CockpitRadio.IsChecked  = _config.CockpitMode;
            StandardRadio.IsChecked = !_config.CockpitMode;
            EnableLogCheckBox.IsChecked = _config.EnableCombatLogMonitor;

            UpdateStep();
        }

        private void UpdateStep()
        {
            StepLabel.Text = $"Step {_step} of {TotalSteps}";

            Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

            BackButton.Visibility = _step > 1 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Content    = _step == TotalSteps ? "Let's Go!" : "Next →";
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_step == TotalSteps)
            {
                SaveSettings();
                DialogResult = true;
                Close();
                return;
            }

            _step++;
            UpdateStep();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_step > 1) { _step--; UpdateStep(); }
        }

        private void BrowseLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use OpenFileDialog pointed at a folder as reliable fallback
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Navigate to your EVE Gamelogs folder — then click Open",
                    InitialDirectory = Directory.Exists(LogFolderBox.Text)
                        ? LogFolderBox.Text
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    FileName = "Select this folder",
                    Filter = "Folder|*.none",
                    CheckFileExists = false,
                    CheckPathExists = true
                };

                if (dlg.ShowDialog(this) == true)
                {
                    // User selected a file — use the directory it's in
                    var folder = System.IO.Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrWhiteSpace(folder))
                        LogFolderBox.Text = folder;
                }
            }
            catch
            {
                System.Windows.MessageBox.Show(
                    "Folder browser unavailable.\n\nPlease type the path manually.\n\nDefault:\n" +
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "EVE", "logs", "Gamelogs"),
                    "Browse unavailable",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void SaveSettings()
        {
            _config.CockpitMode            = CockpitRadio.IsChecked == true;
            _config.CombatLogFolder        = LogFolderBox.Text.Trim();
            _config.EnableCombatLogMonitor = EnableLogCheckBox.IsChecked == true;
            _config.SetupComplete          = true;
            _config.Save();
        }
    }
}
