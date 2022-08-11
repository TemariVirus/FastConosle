using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class FastConsole
{
    #region // Font
    private const int FixedWidthTrueType = 54;
    private const int StandardOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);


    private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FontInfo
    {
        internal int cbSize;
        internal int FontIndex;
        internal short FontWidth;
        public short FontSize;
        public int FontFamily;
        public int FontWeight;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.wc, SizeConst = 32)]
        public string FontName;
    }

    public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
    {
        FontInfo before = new FontInfo
        {
            cbSize = Marshal.SizeOf<FontInfo>()
        };

        if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
        {

            FontInfo set = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>(),
                FontIndex = 0,
                FontFamily = FixedWidthTrueType,
                FontName = font,
                FontWeight = 400,
                FontSize = fontSize > 0 ? fontSize : before.FontSize
            };

            // Get some settings from current font.
            if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
            {
                var ex = Marshal.GetLastWin32Error();
                Console.WriteLine("Error setting font: " + ex);
                throw new System.ComponentModel.Win32Exception(ex);
            }

            FontInfo after = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>()
            };
            GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);

            return new[] { before, set, after };
        }
        else
        {
            var er = Marshal.GetLastWin32Error();
            Console.WriteLine("Get error " + er);
            throw new System.ComponentModel.Win32Exception(er);
        }
    }
    #endregion

    private static int Width, Height;
    private static int XOffset, YOffset;
    public static bool CursorVisible = false;
    public static int CursorLeft = 0, CursorTop = 0;
    public static float Framerate;
    static int[] ConsoleBuffer;
    static SafeFileHandle ConoutHandle;

    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(
        string fileName,
        [MarshalAs(UnmanagedType.U4)] uint fileAccess,
        [MarshalAs(UnmanagedType.U4)] uint fileShare,
        IntPtr securityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
        [MarshalAs(UnmanagedType.U4)] int flags,
        IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteConsoleOutputW(
        SafeFileHandle hConsoleOutput,
        int[] lpBuffer,
        Coord dwBufferSize,
        Coord dwBufferCoord,
        ref Rect lpWriteRegion);

    [StructLayout(LayoutKind.Sequential)]
    struct Coord
    {
        public static readonly Coord Zero = new Coord(0, 0);
        public short X;
        public short Y;

        public Coord(int _x, int _y)
        {
            X = (short)_x;
            Y = (short)_y;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public readonly short Left;
        public readonly short Top;
        public readonly short Right;
        public readonly short Bottom;

        public Rect(int left, int top, int right, int height)
        {
            Left = (short)left;
            Top = (short)top;
            Right = (short)right;
            Bottom = (short)height;
        }
    }

    // Returns false if it failed to grab the CONOUT$ file handle
    // Otherwise, returns true
    public static bool Initialise()
    {
        Width = (short)Console.BufferWidth;
        Height = (short)Console.BufferHeight;

        ConsoleBuffer = new int[Width * Height];
        Console.OutputEncoding = System.Text.Encoding.Unicode;

        ConoutHandle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
        return !ConoutHandle.IsInvalid;
    }

    public static void ConsoleUpdateLoop()
    {
        if (!Initialise()) throw new Exception("Failed to initialise console!");

        while (true)
        {
            try
            {
                Console.BufferWidth = Math.Max(Width, Console.WindowWidth);
                Console.BufferHeight = Math.Max(Height, Console.WindowHeight);
            }
            catch { }
            int old_xoffset = XOffset, old_yoffset = YOffset;
            XOffset = (Console.WindowWidth - Width) / 2;
            YOffset = (Console.WindowHeight - Height) / 2;
            Console.CursorVisible = CursorVisible;
            if (old_xoffset != XOffset || old_yoffset != YOffset)
            {
                // Fill the whole buffer with black
                int width = Console.BufferWidth, height = Console.BufferHeight;
                int[] the_void = new int[width * height];
                Rect void_rect = new Rect(0, 0, width, height);
                WriteConsoleOutputW(ConoutHandle, the_void, new Coord((short)width, (short)height), Coord.Zero, ref void_rect);
            }
            Render();
            Thread.Sleep((int)(1000 / Framerate));
        }
    }

    public static void SetWindow(int width, int height)
    {
        Console.WindowWidth = width;
        Console.WindowHeight = height;
    }

    public static void SetBuffer(int width, int height)
    {
        Console.BufferWidth = width;
        Console.BufferHeight = height;
        ResizeBuffer(width, height);
    }

    public static void Set(int window_width, int window_height, int buffer_width, int buffer_height)
    {
        if (window_width <= Console.BufferWidth) Console.WindowWidth = window_width;
        Console.BufferWidth = buffer_width;
        Console.WindowWidth = window_width;
        if (window_height <= Console.BufferHeight) Console.WindowHeight = window_height;
        Console.BufferHeight = buffer_height;
        Console.WindowHeight = window_height;

        ResizeBuffer(buffer_width, buffer_height);
    }

    static void ResizeBuffer(int width, int height)
    {
        int old_width = Width, old_height = Height;
        Width = (short)width;
        Height = (short)height;
        // Create a new screen buffer and copy characters from the old one
        int[] new_buff = new int[Width * Height];
        int min_height = Math.Min(Height, old_height), min_width = Math.Min(Width, old_width);
        for (int i = 0; i < min_height; i++)
            Buffer.BlockCopy(ConsoleBuffer, sizeof(int) * i * old_width, new_buff, sizeof(int) * i * Width, sizeof(int) * min_width);
        ConsoleBuffer = new_buff;
    }

    public static void Write(string text)
    {
        WriteAt(text, CursorLeft, CursorTop);
    }

    public static void WriteLine(string text)
    {
        WriteAt(text, CursorLeft, CursorTop);
        CursorLeft = 0;
        CursorTop = Math.Min(CursorTop + 1, Width - 1);
    }

    public static void WriteAt(string text, int x, int y, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
    {
        int index = y * Width + x;
        for (int i = 0; i < text.Length && index < Height * Width; i++, index++)
        {
            // might need to do checks for tab, return and newline?
            ConsoleBuffer[index] = text[i] | ((int)foreground << 16) | ((int)background << 20);
        }
        int cursor_pos = Math.Min(index, ConsoleBuffer.Length - 1);
        CursorLeft = cursor_pos % Width;
        CursorTop = cursor_pos / Width;
    }

    public static void Clear()
    {
        ConsoleBuffer = new int[Width * Height];
    }

    static void Render()
    {
        Rect rect = new Rect(XOffset, YOffset, XOffset + Width, YOffset + Height);
        WriteConsoleOutputW(ConoutHandle, ConsoleBuffer, new Coord(Width, Height), Coord.Zero, ref rect);
    }
}
