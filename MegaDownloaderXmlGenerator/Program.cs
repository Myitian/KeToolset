using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MegaDownloaderXmlGenerator;

partial class Program
{
    static readonly KeyValuePair<string, string> v2 = new("v", "2");

    [GeneratedRegex(@"https?://mega(?:\.co)?\.nz/[^""'<>\s]+#[a-zA-Z0-9\-_]+")]
    private static partial Regex RegMega();
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
#pragma warning disable CA1859 // it can be an array or a list, but roslyn bugged here.
        IList<string> directories;
#pragma warning restore CA1859
        if (args.Length > 0)
            directories = args;
        else
        {
            Console.Error.WriteLine($"""
                Usage:
                    MegaDownloaderXmlGenerator <directories...>

                Replace or merge into:
                    {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\MegaDownloader\Config\DownloadList.xml
                
                Interactive input:
                """);
            directories = [];
            while (Console.In.ReadLine() is string line and not "")
                directories.Add(line.AsSpan().Trim().Trim('"').ToString());
        }
        if (directories is ["*DEBUG", ..])
        {
            while (Console.ReadLine() is string line and not "")
            {
                if (!MegaUtils.ExtractIdKey(line.AsMemory(), out ReadOnlyMemory<char> id, out ReadOnlyMemory<char> key))
                    Console.Error.WriteLine("Invalid URL");
                else
                {
                    Console.Error.WriteLine(id.Span);
                    Console.Error.WriteLine(key.Span);
                    if (line.Contains("/folder/"))
                    {
                        foreach ((string fileUrl, string filePath) in await MegaUtils.ResolveFolderAsync(id, key))
                        {
                            Console.Error.WriteLine(fileUrl);
                            Console.Error.WriteLine(filePath);
                        }
                    }
                }
            }
            return 1;
        }
        EnumerationOptions options = new()
        {
            MatchType = MatchType.Simple,
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };
        Regex regMega = RegMega();
        Dictionary<string, List<XmlNode>> folders = [];
        foreach ((string directory, string url) in directories
            .Where(Directory.Exists)
            .SelectMany(it => Directory.EnumerateFiles(it, "*.placeholder.txt", options))
            .Distinct()
            .Select(it => (Path.GetDirectoryName(it) ?? "", regMega.Match(File.ReadAllText(it))))
            .Where(it => it.Item2.Success)
            .Select(it => (it.Item1, it.Item2.Value)))
        {
            Console.Error.WriteLine($"Loading {url}...");
            if (!MegaUtils.ExtractIdKey(url.AsMemory(), out ReadOnlyMemory<char> id, out ReadOnlyMemory<char> key))
                Console.Error.WriteLine("Warning: Invalid URL");
            else
            {
                if (!folders.TryGetValue(directory, out List<XmlNode>? files))
                    folders[directory] = files = [];
                if (!url.Contains("/folder/"))
                    files.Add(CreateFileNode(id.Span, key.Span, directory, url));
                else
                {
                    try
                    {
                        foreach ((string fileUrl, string filePath) in await MegaUtils.ResolveFolderAsync(id, key))
                        {
                            Console.Error.WriteLine($"Loading {fileUrl}...");
                            if (!MegaUtils.ExtractIdKey(fileUrl.AsMemory(), out id, out key))
                                continue;
                            files.Add(CreateFileNode(id.Span, key.Span, directory, fileUrl, Path.GetDirectoryName(filePath)));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                }
            }
            Console.Error.Flush();
        }
        XmlNode root = new("ListaPaquetes");
        foreach ((string folder, List<XmlNode> files) in folders)
        {
            string folderName = Path.GetFileName(folder);
            root.AppendChild(new XmlNode("Paquete")
                .AppendChild(new XmlNode("Nombre").SetValue(folderName))
                .AppendChild(new XmlNode("RutaLocal").SetValue(folder))
                .AppendChild(new XmlNode("CrearSubdirectorio").SetValue("True"))
                .AppendChild(new XmlNode("ExtraccionFicheroAutomatica").SetValue("False"))
                .AppendChild(new XmlNode("ListaFicheros").SetValue(files)));
        }
        using (XmlWriter writer = XmlWriter.Create(Console.Out, new()
        {
            OmitXmlDeclaration = true,
            Indent = true
        }))
            root.WriteTo(writer);
        Console.Error.WriteLine("""

            
            Done!
            """);
        return 0;

        static XmlNode CreateFileNode(ReadOnlySpan<char> id, ReadOnlySpan<char> key, string directory, string url, string? path = null)
        {
            path ??= "";
            return new XmlNode("Fichero")
                .AppendAttribute(v2)
                .AppendChild(new XmlNode("FileID").SetValue(EncryptString(id)))
                .AppendChild(new XmlNode("FileKey").SetValue(EncryptString(key)))
                .AppendChild(new XmlNode("URL").SetValue(EncryptString(url)))
                .AppendChild(new XmlNode("NombreFichero").SetValue(url))
                .AppendChild(new XmlNode("RutaLocal").SetValue(Path.Combine(directory, path)))
                .AppendChild(new XmlNode("RutaRelativa").SetValue(path));
        }
    }
    public static string EncryptString(ReadOnlySpan<char> data)
    {
        ReadOnlySpan<byte> bytes;
        if (BitConverter.IsLittleEndian)
            bytes = MemoryMarshal.AsBytes(data);
        else
        {
            ushort[] chars = new ushort[data.Length];
            BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<char, ushort>(data), chars);
            bytes = MemoryMarshal.AsBytes(chars);
        }
        return Convert.ToBase64String(ProtectedData.Protect(bytes, DataProtectionScope.CurrentUser, Entropy));
    }
    static ReadOnlySpan<byte> Entropy => [
//      G         *         S         N         A         f         h         H
        0x47,0x00,0x2A,0x00,0x53,0x00,0x4E,0x00,0x41,0x00,0x66,0x00,0x68,0x00,0x48,0x00,
//      W         5         A         ¿         A         m         c         k
        0x57,0x00,0x35,0x00,0x41,0x00,0xBF,0x00,0x41,0x00,0x6D,0x00,0x63,0x00,0x6B,0x00,
//      +         X         M         L         C         M         6         M
        0x2B,0x00,0x58,0x00,0x4D,0x00,0x4C,0x00,0x43,0x00,0x4D,0x00,0x36,0x00,0x4D,0x00,
//      #         $         x         E         K         ;         9         q
        0x23,0x00,0x24,0x00,0x78,0x00,0x45,0x00,0x4B,0x00,0x3B,0x00,0x39,0x00,0x71,0x00];
}
