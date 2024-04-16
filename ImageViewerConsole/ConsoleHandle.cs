using System;
using System.Runtime.InteropServices;

namespace Bfx.Console
{
    internal static class ConsoleHandle //az osztály lehetővé teszi az ASCII escape-ek értelmezését a parancssorban
    {
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        public static void handleColorEscape()
            //ez a függvény lehetővé teszi az ASCII escape-ek értelmezését a parancssorban
            //.NET Core 8.0 esetén nem biztos hogy szükséges, de ez egy régebbi projektem felújított változata
            //ami .NET Framework 4.7.2-t használ
        {
            IntPtr StdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(StdOut, out uint outConsoleMode);
            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            SetConsoleMode(StdOut, outConsoleMode);
        }
    }
}
