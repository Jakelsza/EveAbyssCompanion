using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EveAbyssCompanion
{
    /// <summary>
    /// Tails the newest EVE GameLog file and extracts entity/NPC names found in (combat) lines.
    /// The log folder is supplied by the user via config/setup; if none is given,
    /// a sensible default under the current user's Documents folder is used.
    /// </summary>
    public sealed class CombatLogMonitor : IDisposable
    {
        // Default EVE Gamelogs location for the current user. Computed at runtime so it
        // works on any machine and never embeds a personal username/path in the source.
        // This is only a fallback — normally the folder comes from config/setup.
        public static readonly string DefaultLogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EVE", "logs", "Gamelogs");

        private static readonly Regex HtmlFromRegex = new(
            @"from</font>\s*<b><color=0x[0-9a-fA-F]+>(?<n>[^<]+)</b>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex HtmlToRegex = new(
            @"to</font>\s*<b><color=0x[0-9a-fA-F]+>(?<n>[^<]+)</b>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PlainCombatNameRegex = new(
            @"\]\s+\(combat\)\s+(?<n>[^-]+?)\s+(misses|hits|grazes|smashes|wrecks|penetrates|glances)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly string _folder;
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        private string? _currentFile;
        private long _position;
        private DateTime _currentFileLastWriteUtc;

        /// <summary>
        /// Raised when a new entity/NPC name is detected.
        /// </summary>
        public event Action<string>? EntitySeen;

        /// <summary>
        /// Backwards-compatible alias expected by older MainWindow code.
        /// </summary>
        public event Action<string>? NpcSeen
        {
            add { EntitySeen += value; }
            remove { EntitySeen -= value; }
        }

        /// <summary>
        /// Backwards-compatible start method expected by older MainWindow code.
        /// </summary>
        public void StartMonitoring(bool ignoreExistingLines = true) => Start(ignoreExistingLines);

        /// <summary>
        /// Backwards-compatible stop method expected by older MainWindow code.
        /// </summary>
        public void StopMonitoring() => Stop();

        public CombatLogMonitor(string? folderPath = null)
        {
            _folder = string.IsNullOrWhiteSpace(folderPath) ? DefaultLogFolder : folderPath;
        }

        public string LogFolder => _folder;

        public void Start(bool ignoreExistingLines = true)
        {
            _seen.Clear();
            SelectNewestFile();

            if (_currentFile == null)
                return;

            try
            {
                var fi = new FileInfo(_currentFile);
                _position = ignoreExistingLines ? fi.Length : 0;
                _currentFileLastWriteUtc = fi.LastWriteTimeUtc;
            }
            catch
            {
                _position = 0;
            }
        }

        public void Stop()
        {
            _currentFile = null;
            _position = 0;
            _currentFileLastWriteUtc = default;
            _seen.Clear();
        }

        public void ResetRoom()
        {
            // When moving to a new room we want a fresh per-room list in the UI.
            _seen.Clear();
        }

        public void Poll()
        {
            if (!Directory.Exists(_folder))
                return;

            if (_currentFile == null)
            {
                SelectNewestFile();
                if (_currentFile == null)
                    return;

                try
                {
                    _position = new FileInfo(_currentFile).Length;
                    _currentFileLastWriteUtc = File.GetLastWriteTimeUtc(_currentFile);
                }
                catch
                {
                    _position = 0;
                }
            }

            MaybeSwitchToNewerFile();

            if (_currentFile == null)
                return;

            try
            {
                using var fs = new FileStream(_currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                if (_position > fs.Length)
                    _position = 0;

                fs.Seek(_position, SeekOrigin.Begin);
                using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    _position = fs.Position;
                    ProcessLine(line);
                }
            }
            catch
            {
                // Ignore read errors (log file being rotated, temporary file lock, etc.).
            }
        }

        private void SelectNewestFile()
        {
            var newest = FindNewestLogFile();
            _currentFile = newest;
            _position = 0;
            _currentFileLastWriteUtc = default;
        }

        private void MaybeSwitchToNewerFile()
        {
            if (_currentFile == null)
                return;

            string? newest = FindNewestLogFile();
            if (newest == null)
                return;

            if (string.Equals(newest, _currentFile, StringComparison.OrdinalIgnoreCase))
            {
                try { _currentFileLastWriteUtc = File.GetLastWriteTimeUtc(_currentFile); } catch { }
                return;
            }

            DateTime newestWriteUtc;
            try { newestWriteUtc = File.GetLastWriteTimeUtc(newest); }
            catch { return; }

            // Only switch if the new file is meaningfully newer than the current file.
            if (newestWriteUtc <= _currentFileLastWriteUtc.AddSeconds(1))
                return;

            _currentFile = newest;
            _currentFileLastWriteUtc = newestWriteUtc;

            try { _position = new FileInfo(_currentFile).Length; }
            catch { _position = 0; }

            _seen.Clear();
        }

        private string? FindNewestLogFile()
        {
            try
            {
                var dir = new DirectoryInfo(_folder);
                if (!dir.Exists)
                    return null;

                FileInfo? best = null;
                foreach (var fi in dir.GetFiles("*.txt"))
                {
                    if (best == null || fi.LastWriteTimeUtc > best.LastWriteTimeUtc)
                        best = fi;
                }

                return best?.FullName;
            }
            catch
            {
                return null;
            }
        }

        private void ProcessLine(string line)
        {
            if (!line.Contains("(combat)", StringComparison.Ordinal))
                return;

            foreach (var name in ExtractNames(line))
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var cleaned = name.Trim();

                // Filter out your own lines.
                if (cleaned.StartsWith("Your ", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_seen.Add(cleaned))
                    EntitySeen?.Invoke(cleaned);
            }
        }

        private static IEnumerable<string> ExtractNames(string line)
        {
            // 1) HTML formatted combat lines
            var fromMatch = HtmlFromRegex.Match(line);
            if (fromMatch.Success)
                yield return fromMatch.Groups["n"].Value;

            var toMatch = HtmlToRegex.Match(line);
            if (toMatch.Success)
                yield return toMatch.Groups["n"].Value;

            // 2) Plain text combat lines
            var plain = PlainCombatNameRegex.Match(line);
            if (plain.Success)
                yield return plain.Groups["n"].Value;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
