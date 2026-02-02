using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MegaDownloaderTaskReset;

[DebuggerDisplay("Foreground = {Foreground}, Background = {Background}, ResetColor = {ResetColor}, ANSI = {ToString()}")]
struct ConColor
{
    public static ReadOnlySpan<byte> ConsoleColorToAnsiCode => [
        00, // Black,
        04, // DarkBlue,
        02, // DarkGreen,
        06, // DarkCyan,
        01, // DarkRed,
        05, // DarkMagenta,
        03, // DarkYellow,
        07, // Gray,
        
        60, // DarkGray,
        64, // Blue,
        62, // Green,
        66, // Cyan,
        61, // Red,
        65, // Magenta,
        63, // Yellow,
        67, // White
    ];
    public const ConsoleColor UnknownColor = (ConsoleColor)(-1);
    public ConsoleColor Foreground;
    public ConsoleColor Background;
    public bool ResetColor;
    public readonly void Apply()
    {
        if (ResetColor)
            Console.ResetColor();
        if (Foreground is not UnknownColor)
            Console.ForegroundColor = Foreground;
        if (Background is not UnknownColor)
            Console.BackgroundColor = Background;
    }
    public override readonly string ToString()
    {
        if (Foreground is UnknownColor && Background is UnknownColor)
            return ResetColor ? $"\e[39;49m" : "";

        // 10 chars max
        DefaultInterpolatedStringHandler valueStringBuilder = new(0, 0, null, stackalloc char[16]);
        valueStringBuilder.AppendLiteral("\e[");
        bool first = true;
        if (Foreground is UnknownColor)
        {
            if (ResetColor)
            {
                valueStringBuilder.AppendLiteral("39");
                first = false;
            }
        }
        else
        {
            int fg = ConsoleColorToAnsiCode[(int)Foreground & 0xF] + 30;
            valueStringBuilder.AppendFormatted(fg);
            first = false;
        }
        if (Background is UnknownColor)
        {
            if (ResetColor)
                valueStringBuilder.AppendLiteral(first ? "49" : ";49");
        }
        else
        {
            if (!first)
                valueStringBuilder.AppendLiteral(";");
            int bg = ConsoleColorToAnsiCode[(int)Background & 0xF] + 30;
            valueStringBuilder.AppendFormatted(bg);
        }
        valueStringBuilder.AppendLiteral("m");
        return valueStringBuilder.ToString();
    }
    public static ConColor Reset => new()
    {
        Foreground = UnknownColor,
        Background = UnknownColor,
        ResetColor = true
    };
    public static ConColor FG(ConsoleColor value, bool reset) => new()
    {
        Foreground = value,
        Background = UnknownColor,
        ResetColor = reset
    };
    public static ConColor BlackFG => FG(ConsoleColor.Black, false);
    public static ConColor DarkBlueFG => FG(ConsoleColor.DarkBlue, false);
    public static ConColor DarkGreenFG => FG(ConsoleColor.DarkGreen, false);
    public static ConColor DarkCyanFG => FG(ConsoleColor.DarkCyan, false);
    public static ConColor DarkRedFG => FG(ConsoleColor.DarkRed, false);
    public static ConColor DarkMagentaFG => FG(ConsoleColor.DarkMagenta, false);
    public static ConColor DarkYellowFG => FG(ConsoleColor.DarkYellow, false);
    public static ConColor GrayFG => FG(ConsoleColor.Gray, false);
    public static ConColor DarkGrayFG => FG(ConsoleColor.DarkGray, false);
    public static ConColor BlueFG => FG(ConsoleColor.Blue, false);
    public static ConColor GreenFG => FG(ConsoleColor.Green, false);
    public static ConColor CyanFG => FG(ConsoleColor.Cyan, false);
    public static ConColor RedFG => FG(ConsoleColor.Red, false);
    public static ConColor MagentaFG => FG(ConsoleColor.Magenta, false);
    public static ConColor YellowFG => FG(ConsoleColor.Yellow, false);
    public static ConColor WhiteFG => FG(ConsoleColor.White, false);
    public static ConColor BG(ConsoleColor value, bool reset) => new()
    {
        Foreground = UnknownColor,
        Background = value,
        ResetColor = reset
    };
    public static ConColor BlackBG => BG(ConsoleColor.Black, false);
    public static ConColor DarkBlueBG => BG(ConsoleColor.DarkBlue, false);
    public static ConColor DarkGreenBG => BG(ConsoleColor.DarkGreen, false);
    public static ConColor DarkCyanBG => BG(ConsoleColor.DarkCyan, false);
    public static ConColor DarkRedBG => BG(ConsoleColor.DarkRed, false);
    public static ConColor DarkMagentaBG => BG(ConsoleColor.DarkMagenta, false);
    public static ConColor DarkYellowBG => BG(ConsoleColor.DarkYellow, false);
    public static ConColor GrayBG => BG(ConsoleColor.Gray, false);
    public static ConColor DarkGrayBG => BG(ConsoleColor.DarkGray, false);
    public static ConColor BlueBG => BG(ConsoleColor.Blue, false);
    public static ConColor GreenBG => BG(ConsoleColor.Green, false);
    public static ConColor CyanBG => BG(ConsoleColor.Cyan, false);
    public static ConColor RedBG => BG(ConsoleColor.Red, false);
    public static ConColor MagentaBG => BG(ConsoleColor.Magenta, false);
    public static ConColor YellowBG => BG(ConsoleColor.Yellow, false);
    public static ConColor WhiteBG => BG(ConsoleColor.White, false);
    public static void Write(
        TextWriter writer,
        [InterpolatedStringHandlerArgument(nameof(writer))] InterpolationHandler handler)
    {
    }
    public static void WriteLine(
        TextWriter writer,
        [InterpolatedStringHandlerArgument(nameof(writer))] InterpolationHandler handler)
    {
        writer.WriteLine();
    }

    [InterpolatedStringHandler]
    internal readonly struct InterpolationHandler
    {
        private readonly TextWriter _writer;

        public InterpolationHandler(int literalLength, int formattedCount, TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            _writer = writer;
        }
        public void AppendLiteral(string value)
        {
            _writer.Write(value);
        }
        public void AppendFormatted<T>(T value)
        {
            if (value is ConColor color)
                color.Apply();
            else
                _writer.Write(value?.ToString());
        }
        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            _writer.Write(value);
        }
    }
}