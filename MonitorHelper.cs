using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace EveAbyssCompanion
{
    internal static class MonitorHelper
    {
        internal sealed class Monitor
        {
            public Rect PixelWorkArea { get; init; }
            public bool IsPrimary { get; init; }
        }

        public static List<Monitor> GetMonitors()
        {
            var list = new List<Monitor>();

            bool Callback(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData)
            {
                var mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf<MONITORINFO>();
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    var work = mi.rcWork;
                    list.Add(new Monitor
                    {
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                        PixelWorkArea = new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top)
                    });
                }
                return true;
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
            return list;
        }

        private const int MONITORINFOF_PRIMARY = 0x00000001;

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
