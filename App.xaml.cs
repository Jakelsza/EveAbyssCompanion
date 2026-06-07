using System;
using System.Windows;

namespace EveAbyssCompanion
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch unhandled exceptions and show a useful message instead of silent crash
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var msg = ex.ExceptionObject?.ToString() ?? "Unknown error";

                // Check for the most common cause: missing .NET 8 Desktop Runtime
                // (This won't fire for missing runtime — the process won't start at all —
                //  but catches other startup failures with a helpful message.)
                MessageBox.Show(
                    "EVE Abyss Companion encountered a startup error.\n\n" +
                    "Most common cause: .NET 8 Desktop Runtime not installed.\n\n" +
                    "Download it free from:\n" +
                    "https://dotnet.microsoft.com/en-us/download/dotnet/8.0\n\n" +
                    "Choose: .NET Desktop Runtime 8.0 (Windows x64)\n\n" +
                    "--- Error detail ---\n" + msg,
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            base.OnStartup(e);
        }
    }
}
