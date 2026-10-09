using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EvaGest.Helpers;

/// <summary>
/// What a second launch does when EvaGest is already open (CU-12). It used to just exit,
/// so double-clicking the icon while the app sat minimised or behind another window
/// looked like nothing happening at all. Now it brings the open window to the front.
/// </summary>
public static class SingleInstance
{
    private const int SwRestore = 9;

    /// <summary>
    /// Finds the other running EvaGest and brings its main window forward, restoring it
    /// if it is minimised. Returns false when there is no window to show (the other copy
    /// is still starting up, or has none), so the caller can say so instead.
    /// Windows lets this process take the foreground because the user just launched it.
    /// </summary>
    public static bool ActivateExisting()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            using (other)
            {
                if (other.Id == current.Id) continue;

                // The other copy can exit between the listing and this read, and then
                // MainWindowHandle throws. Escaping from here left this second copy with
                // no window and no shutdown, alive in the background (E-02).
                IntPtr window;
                try
                {
                    window = other.MainWindowHandle;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                if (window == IntPtr.Zero) continue;

                if (IsIconic(window)) ShowWindow(window, SwRestore);
                return SetForegroundWindow(window);
            }
        }
        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);
}
