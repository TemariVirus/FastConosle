using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;

static class FastConsole
{
    static short Width, Height;
    public static int Cursor_Left = 0, Cursor_Top = 0;
    static int[] Screen_Buffer;
    static SafeFileHandle Conout_Handle;

    static void Main(string[] args)
    {
        if (!Initialise()) return;

        // write whatever u want
        WriteAt("asdasd", 10, 6);
        Render();
        
        // to stop the text at the end from covering up what u wrote
        Console.ReadKey(true);
    }

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
    public struct Coord
    {
        public static readonly Coord Zero = new Coord(0, 0);
        public short X;
        public short Y;

        public Coord(short _x, short _y)
        {
            X = _x;
            Y = _y;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
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
    static bool Initialise()
    {
        Width = (short)Console.BufferWidth;
        Height = (short)Console.BufferHeight;
        Screen_Buffer = new int[Width * Height];
        Console.OutputEncoding = System.Text.Encoding.Unicode;

        Conout_Handle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
        return !Conout_Handle.IsInvalid;
    }

    // Sets the current window dimensions
    static void SetWindow(int width, int height)
    {
        Console.WindowWidth = width;
        Console.WindowHeight = height;
    }

    // Sets the current buffer dimensions
    static void SetBuffer(int width, int height)
    {
        Console.BufferWidth = width;
        Console.BufferHeight = height;
        int old_width = Width, old_height = Height;
        Width = (short)width;
        Height = (short)height;
        // Create a new screen buffer and copy characters from the old one
        int[] new_buff = new int[Width * Height];
        int min_height = Math.Min(Height, old_height), min_width = Math.Min(Width, old_width);
        for (int i = 0; i < min_height; i++)
        {
            Buffer.BlockCopy(Screen_Buffer, 4 * i * old_width, new_buff, 4 * i * Width, 4 * min_width);
        }
        Screen_Buffer = new_buff;
    }

    // Sets the current window and buffer dimentions
    static void Set(int window_width, int window_height, int buffer_width, int buffer_height)
    {
        if (window_width > buffer_width || window_height > buffer_height)
        {
            throw new ArgumentException("Window dimensions must not exceed buffer dimensions");
        }

        if (window_width <= Width)
        {
            Console.WindowWidth = window_width;
            Console.BufferWidth = buffer_width;
        }
        else
        {
            Console.BufferWidth = buffer_width;
            Console.WindowWidth = window_width;
        }
        if (window_height <= Height)
        {
            Console.WindowHeight = window_height;
            Console.BufferHeight = buffer_height;
        }
        else
        {
            Console.BufferHeight = buffer_height;
            Console.WindowHeight = window_height;
        }
        int old_width = Width, old_height = Height;
        Width = (short)buffer_width;
        Height = (short)buffer_height;
        // Create a new screen buffer and copy characters from the old one
        int[] new_buff = new int[Width * Height];
        int min_height = Math.Min(Height, old_height), min_width = Math.Min(Width, old_width);
        for (int i = 0; i < min_height; i++)
        {
            Buffer.BlockCopy(Screen_Buffer, 4 * i * old_width, new_buff, 4 * i * Width, 4 * min_width);
        }
        Screen_Buffer = new_buff;
    }

    // Writes a string of text at the current cursor position
    static void Write(string text)
    {
        WriteAt(text, Cursor_Left, Cursor_Top);
    }

    // Writes a string of text at the current cursor position and moves the cursor to the next line
    static void WriteLine(string text)
    {
        WriteAt(text, Cursor_Left, Cursor_Top);
        Cursor_Left = 0;
        Cursor_Top = Math.Min(Cursor_Top + 1, Width - 1);
    }

    // Writes a string of text onto the x and y position of the screen buffer
    static void WriteAt(string text, int x, int y, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
    {
        int index = y * Width + x;
        for (int i = 0; i < text.Length && index < Height * Width; i++, index++)
        {
            Screen_Buffer[index] = text[i] | ((int)foreground << 16) | ((int)background << 20);
        }
        int cursor_pos = Math.Min(index, Screen_Buffer.Length - 1);
        Cursor_Left = cursor_pos % Width;
        Cursor_Top = cursor_pos / Width;
    }

    // Renders the contents of the screen buffer
    static void Render()
    {
        Rect rect = new Rect(0, 0, Width, Height);
        WriteConsoleOutputW(Conout_Handle, Screen_Buffer, new Coord(Width, Height), Coord.Zero, ref rect);
    }
}