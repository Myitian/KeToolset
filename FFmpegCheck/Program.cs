using SimpleArgs;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FFmpegCheck;

class Program
{
    static readonly string NullDevice = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";
    static readonly string[] BasicArgs = [
        "-hide_banner",
        "-err_detect",
        "explode",
        "-xerror",
        "-v",
        "error",
        "-stats"];
    static readonly EnumerationOptions options = new()
    {
        // When RecurseSubdirectories = true, the return order does not conform to the design expectation.
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple
    };
    static int Main(string[] args)
    {
        try
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            ArgParser argx = new(args, true,
                new("--help", 0, "-h")
                {
                    Info = "help"
                },
                new("--output", 1, "-o")
                {
                    Info = "path: report output (optional, default: \"report.{UnixTimeMilliseconds}.txt\")"
                },
                new("--filter", 1, "-f")
                {
                    Info = "regex: path filter (optional)"
                },
                new("--resume", 1, "-r")
                {
                    Info = "path: resume from (optional)"
                },
                new("--hwaccel", 1, "-hw")
                {
                    Info = "path: ffmpeg hwaccel (optional)"
                },
                new("--decoder", 1, "-d")
                {
                    Info = "path: ffmpeg codec:v (optional)"
                },
                new("--dry-run", 1, "-dry")
                {
                    Info = "boolean: output file list only (optional, default: false)"
                });
            if (argx.Results.ContainsKey("--help") || argx.UnknownArgs.Count == 0)
            {
                argx.WriteHelp(Console.Error);
                Console.Error.WriteLine("""


                    remaining arguments: input files/folders
                    """);
                return 1;
            }
            Regex? path_filter = null;
            ReadOnlySpan<char> resume_checkpoint = [];
            if (!argx.TryGetString("--output", out string? output))
                output = $"report.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
            if (argx.TryGetString("--filter", out string? filter))
                path_filter = new(filter, RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (argx.TryGetString("--resume", out string? resume))
                resume_checkpoint = resume;
            if (!argx.TryGetString("--hwaccel", out string? hwaccel))
                hwaccel = null;
            if (!argx.TryGetString("--decoder", out string? decoder))
                decoder = null;
            if (!argx.TryGetBoolean("--dry-run", out bool dry))
                dry = false;
            if (Path.GetDirectoryName(output) is string directory and not "")
                Directory.CreateDirectory(directory);
            bool waiting_checkpoint = !resume_checkpoint.IsEmpty;
            using FileStream fs = File.Open(output, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter sw = new(fs, Encoding.UTF8);
            foreach (string file in argx.UnknownArgs
                .SelectMany(it => File.Exists(it) ? [Path.GetFullPath(it)]
                                : Directory.Exists(it) ? EnumerateAllFilesDefaultOrder(it)
                                : [])
                .Distinct()
                .Where(it => path_filter?.IsMatch(it) is not false))
            {
                if (waiting_checkpoint)
                {
                    if (!resume_checkpoint.SequenceEqual(file))
                        continue;
                    waiting_checkpoint = false;
                }
                Console.ResetColor();
                Console.Error.WriteLine(file);
                if (dry)
                {
                    sw.WriteLine(file);
                    sw.Flush();
                }
                else
                {
                    using Process? process = Process.Start(CreateProcessStartInfo(file, hwaccel, decoder));
                    process?.WaitForExit();
                    if (process?.ExitCode is 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Error.WriteLine("OK");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("ERROR");
                        sw.WriteLine(file);
                        sw.Flush();
                    }
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            Console.ResetColor();
        }
    }
    static IEnumerable<string> EnumerateAllFilesDefaultOrder(string directory)
    {
        foreach (string fsi in Directory.EnumerateFileSystemEntries(directory, "*", options))
        {
            if (File.Exists(fsi))
                yield return fsi;
            else if (Directory.Exists(fsi))
            {
                foreach (string child in EnumerateAllFilesDefaultOrder(fsi))
                    yield return child;
            }
        }
    }
    static ProcessStartInfo CreateProcessStartInfo(string file, string? hwaccel = null, string? decoder = null)
    {
        ProcessStartInfo psi = new("ffmpeg", BasicArgs)
        {
            UseShellExecute = false,
            CreateNoWindow = false
        };

        if (hwaccel is not null)
        {
            psi.ArgumentList.Add("-hwaccel");
            psi.ArgumentList.Add(hwaccel);
        }
        if (decoder is not null)
        {
            psi.ArgumentList.Add("-codec:v");
            psi.ArgumentList.Add(decoder);
        }
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(file);
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("rawvideo");
        psi.ArgumentList.Add(NullDevice);
        return psi;
    }
}
