using System.Diagnostics;
using System.IO.Compression;

namespace UnzipAll;

static class Program
{
    static void Main(string[] args)
    {
        while (Console.ReadLine() is string path)
        {
            string dir = Path.Join(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            if (Directory.Exists(dir))
                Console.WriteLine($"DIR EXISTS: {dir}");
            else
                SevenZipExtract(dir, path, "hongbai");
        }
    }
    static void SevenZipExtract(string folder, string path, string? password = null)
    {
        ProcessStartInfo _7z = new("7z", [
            "x",
            $"-o{folder}",
            .. password is not null ? [$"-p{password}"] : Enumerable.Empty<string>(),
            path]);
        Process.Start(_7z)?.WaitForExit();
    }
}
