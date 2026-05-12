using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace LeagueSandbox.GameServerConsole;

internal static class Banner {
    private const string ResourceName = "GameServerConsole.Banner.txt";

    public static void Print() {
        try {
            if (OperatingSystem.IsWindows())
                EnableVirtualTerminalOnWindows();

            Console.OutputEncoding = Encoding.UTF8;

            var assembly = typeof(Banner).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            Console.Write(reader.ReadToEnd());
            Console.WriteLine();
        } catch {
            // Banner is cosmetic; never block startup.
        }
    }

    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private static void EnableVirtualTerminalOnWindows() {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == IntPtr.Zero || !GetConsoleMode(handle, out var mode)) return;
        SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }
}
