using KeCore.API;
using KeSpider.OutlinkHandlers;
using KeSpider.ZipEncodingDetector;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UtfUnknown;

namespace KeSpider;

sealed class PostContext(
    HttpClient client,
    Encoding? encoding,
    Dictionary<Array256bit, string> downloadCache,
    PostRoot post,
    DateTime datetime,
    DateTime datetimeEdited,
    string pageFolderPath)
{
    public HttpClient Client { get; } = client;
    public Encoding? Encoding { get; } = encoding;
    public Dictionary<Array256bit, string> DownloadCache { get; } = downloadCache;
    public PostRoot Post { get; } = post;
    public DateTime Datetime { get; } = datetime;
    public DateTime DatetimeEdited { get; } = datetimeEdited;
    public string PageFolderPath { get; } = pageFolderPath;
    public Dictionary<string, (string Path, string? Password)> ArchiveParts { get; } = [];
    public int FileCounter { get; set; }
    public int OutlinkCounter { get; set; }
    public int PictureCounter { get; set; }
    public static void Log(ReadOnlySpan<char> mode, ReadOnlySpan<char> message)
    {
        Console.WriteLine($"    @{mode} - {message}");
    }
    public static bool SkipDownloadIfAlreadyDone(string path, ReadOnlySpan<char> mode)
    {
        if (File.Exists(path) && Program.SaveModePicture == SaveMode.Skip)
        {
            Log(mode, "Skipped");
            return true;
        }
        return false;
    }
    public bool ShouldSkipIfExtracted(
        string fileName,
        int extLen,
        [NotNullIfNotNull(nameof(pathNoExt))] out string? fileNameNoExt,
        [NotNullIfNotNull(nameof(fileNameNoExt))] out string? pathNoExt)
    {
        if (extLen > 0)
        {
            fileNameNoExt = fileName[..^extLen];
            pathNoExt = Path.Combine(PageFolderPath, fileNameNoExt);
            return Directory.Exists(pathNoExt);
        }
        fileNameNoExt = pathNoExt = null;
        return false;
    }
    public bool PrepareDownload(
        ReadOnlySpan<char> name,
        int index,
        out string fileName,
        out string path,
        [NotNullIfNotNull(nameof(pathNoExt))] out string? fileNameNoExt,
        [NotNullIfNotNull(nameof(fileNameNoExt))] out string? pathNoExt,
        ReadOnlySpan<char> mode,
        ReadOnlySpan<char> seqPrefix)
    {
        string fileNameMain = Utils.ReplaceInvalidFileNameChars(name);
        string unprefixedName = Program.FixSpecialExt(fileNameMain);
        string prefixedName = $"{seqPrefix}{index:D3}_{unprefixedName}";
        int extLen = Program.ProcessArchiveName(unprefixedName);
        string finalName = fileName = extLen == 0 ? prefixedName : unprefixedName;
        path = Path.Combine(PageFolderPath, finalName);
        string ext = Path.GetExtension(unprefixedName);
        bool first = !File.Exists(path);
        Log(mode, $"Save as {finalName}");
        foreach (string oldName in Directory.EnumerateFiles(PageFolderPath, $"{seqPrefix}{index}_*", Program.simpleNonRecursiveEnumeration)
                           .Concat(Directory.EnumerateFiles(PageFolderPath, $"{seqPrefix}{index:D3}_*", Program.simpleNonRecursiveEnumeration))
                           .Concat(Directory.EnumerateFiles(PageFolderPath, $"{index}_*{ext}", Program.simpleNonRecursiveEnumeration))
                           .Concat(Directory.EnumerateFiles(PageFolderPath, $"{index:D3}_*{ext}", Program.simpleNonRecursiveEnumeration))
                           .Concat(Directory.EnumerateFiles(PageFolderPath, unprefixedName, Program.simpleNonRecursiveEnumeration))
                           .Where(it => !Path.GetFileName(it.AsSpan()).Equals(finalName, StringComparison.Ordinal))
                           .Where(it => !it.AsSpan().EndsWith(".aria2", StringComparison.OrdinalIgnoreCase)))
        {
            if (first)
            {
                first = false;
                Log(mode, "Fix old name!");
                File.Move(oldName, path);
            }
            else
            {
                Log(mode, "Delete extra old name!");
                File.Delete(oldName);
            }
        }
        return ShouldSkipIfExtracted(unprefixedName, extLen, out fileNameNoExt, out pathNoExt);
    }

    public void ProcessArchives()
    {
        const string MODE = "ARCHIVE";
        foreach ((string p1, (string path, string? password)) in ArchiveParts)
        {
            if (!Directory.Exists(p1) && !File.Exists(p1))
            {
                string? pwd = password;
                switch (pwd)
                {
                    case null when path.EndsWith(".zip"):
                        try
                        {
                            Log(MODE, "zip! Trying to use .NET built-in API...");
                            using ZipArchive zip0 = ZipFile.OpenRead(path);
                            Log(MODE, "zip! Detecting encoding...");
                            DetectionResult result = zip0.DetectEncoding();
                            DetectionDetail? detected = result.Detected;
                            foreach (DetectionDetail detail in result.Details)
                                Log(MODE, $"CONF:{detail.Confidence} E:{detail.EncodingName}");
                            Encoding u;
                            switch (detected?.EncodingName)
                            {
                                case "utf-8":
                                case "ascii":
                                case "gb18030" when detected.Confidence > 0.7f:
                                case "big5" when detected.Confidence > 0.7f:
                                case "shift_jis" when detected.Confidence > 0.7f:
                                    Log(MODE, $"Result: {(u = detected.Encoding).EncodingName}");
                                    break;
                                default:
                                    if (Encoding is null)
                                    {
                                        Log(MODE, "zip! No suitable encoding, fallback to 7z");
                                        goto default;
                                    }
                                    Log(MODE, $"zip! OverridenBy: {(u = Encoding).EncodingName}");
                                    break;
                            }
                            ZipFile.ExtractToDirectory(path, p1, u, true);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Log(MODE, $"Exception: {ex.Message}");
                            goto default;
                        }
                    case "":
                        Log(MODE, "Need password!");
                        pwd = Console.ReadLine();
                        goto default;
                    default:
                        if (pwd is null)
                            Log(MODE, $"7z! \"{p1}\" \"{path}\"");
                        else
                            Log(MODE, $"7z! \"{p1}\" \"{path}\" pwd\"{pwd}\"");
                        Program.SevenZipExtract(p1, path, pwd);
                        break;
                }
            }
        }
    }
    public void DownloadJson(ReadOnlySpan<byte> json)
    {
        const string MODE = "JSON";
        Log(MODE, "Save JSON");
        Utils.SaveFile(json, "post.json", PageFolderPath, Datetime, DatetimeEdited, Program.SaveModeJson);
    }
    public void DownloadContent(string content)
    {
        const string MODE = "CONTENT";
        Log(MODE, "Save Content");

        string path = Path.Combine(PageFolderPath, "content.html");
        if (Program.SaveModeContent == SaveMode.Skip && File.Exists(path))
        {
            Log(MODE, "Skipped");
            Utils.SetTime(path, Datetime, DatetimeEdited);
        }
        else
        {
            Utils.SaveFile(content, "content.html", PageFolderPath, Datetime, DatetimeEdited, Program.SaveModeContent);
        }
    }
    public async Task DownloadFile(string domain)
    {
        const string MODE = "FILE";
        if (Post?.Attachments.IsDefaultOrEmpty is not false)
            return;
        int j = FileCounter;
        foreach (Attachment file in Post.Attachments)
        {
            try
            {
                int index = FileCounter++;
                string fileUrl = $"{file.Server}/data{file.Path}";
                Log(MODE, $"{++j} of {Post.Attachments.Length} - Download File {fileUrl}");
                ReadOnlySpan<char> name = string.IsNullOrEmpty(file.Name) || file.Name.StartsWith("https://www.patreon.com/media-u/", StringComparison.Ordinal)
                    ? Path.GetFileName((ReadOnlySpan<char>)file.Path) : file.Name;
                if (PrepareDownload(
                    name,
                    index,
                    out string fileName,
                    out string path,
                    out string? fileNameNoExt,
                    out string? pathNoExt,
                    MODE,
                    "f"))
                {
                    Log(MODE, "Pass Extracted Archive File!");
                    continue;
                }
                Array256bit sha256 = new();
                Convert.FromHexString(file.Path.AsSpan(7, 64), sha256, out _, out _);
                if (!SkipDownloadIfAlreadyDone(path, in sha256, MODE))
                {
                    Log(MODE, "aria2c!");
                    Program.Aria2cDownload(PageFolderPath, fileName, fileUrl);
                }
                DownloadCache[sha256] = path;
                Utils.SetTime(path, Datetime, DatetimeEdited);
                if (fileNameNoExt is not null
                    && pathNoExt is not null
                    && !Directory.Exists(pathNoExt)
                    && !File.Exists(pathNoExt))
                {
                    bool first = true;
                    bool any = false;
                    foreach (string oldName in Directory.EnumerateDirectories(PageFolderPath, $"{index}_{fileNameNoExt}", Program.simpleNonRecursiveEnumeration)
                                       .Concat(Directory.EnumerateDirectories(PageFolderPath, $"{index:D3}_{fileNameNoExt}", Program.simpleNonRecursiveEnumeration))
                                       .Concat(Directory.EnumerateDirectories(PageFolderPath, fileNameNoExt, Program.simpleNonRecursiveEnumeration))
                                       .Where(it => !Path.GetFileName(it.AsSpan()).Equals(fileNameNoExt, StringComparison.Ordinal)))
                    {
                        if (first)
                        {
                            first = false;
                            Log(MODE, "Fix old extracted archive!");
                            Directory.Move(oldName, pathNoExt);
                        }
                        else
                        {
                            Log(MODE, "Delete extra old extracted archive!");
                            Directory.Delete(oldName, true);
                        }
                        any = true;
                    }
                    if (!any && Path.GetExtension(path).ToLowerInvariant()
                        is ".zip" or ".rar" or ".7z" or ".gz" or ".tar" or ".r00" or ".001")
                    {
                        Archive archive = await Archive.Request(Client, domain, file.Stem).C();
                        string? password = archive.Password;
                        ArchiveParts.Add(pathNoExt, (path, password));
                    }
                }
            }
            catch (Exception ex)
            {
                Log(MODE, $"Exception: {ex.GetType()} {ex.Message}");
            }
        }
    }
    public void DownloadPicture()
    {
        const string MODE = "PICTURE";
        if (Post?.Previews.IsDefaultOrEmpty is not false)
            return;
        int localCounter = 0;
        foreach (Attachment picture in Post.Previews)
        {
            if (string.IsNullOrEmpty(picture.Path) || string.IsNullOrEmpty(picture.Server))
                continue;
            try
            {
                int index = PictureCounter++;
                string fileUrl = $"{picture.Server}/data{picture.Path}";
                Log(MODE, $"{++localCounter} of {Post.Previews.Length} - Download Picture {fileUrl}");
                ReadOnlySpan<char> name = string.IsNullOrEmpty(picture.Name)
                    || picture.Name.StartsWith("https://www.patreon.com/media-u/", StringComparison.Ordinal) // workaround for some strange names
                    ? Path.GetFileName(picture.Path.AsSpan()) : picture.Name;
                if (PrepareDownload(name, index, out string fileName, out string path, out _, out _, MODE, "p"))
                {
                    Log(MODE, "Unexpected archive in picture mode");
                    continue;
                }
                Array256bit sha256 = new();
                Convert.FromHexString(picture.Path.AsSpan(7, 64), sha256, out _, out _);
                if (!SkipDownloadIfAlreadyDone(path, in sha256, MODE))
                {
                    Log(MODE, "aria2c!");
                    Program.Aria2cDownload(PageFolderPath, fileName, fileUrl);
                }
                DownloadCache[sha256] = path;
                Utils.SetTime(path, Datetime, DatetimeEdited);
            }
            catch (Exception ex)
            {
                Log(MODE, $"Exception: {ex.GetType()} {ex.Message}");
            }
        }

    }
    public bool SkipDownloadIfAlreadyDone(string path, in Array256bit sha256, ReadOnlySpan<char> mode)
    {
        if (DownloadCache.TryGetValue(sha256, out string? duplicated))
        {
            if (File.Exists(path))
                File.Delete(path);
            Log(mode, "Link");
            Utils.MakeLink(path, duplicated);
            return true;
        }
        else if (File.Exists(path))
        {
            switch (Program.SaveModePicture)
            {
                case SaveMode.Skip:
                    Log(mode, "Skipped");
                    return true;
                case SaveMode.Replace:
                    using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Array256bit sha256local = new();
                        SHA256.HashData(fs, sha256local);
                        if (sha256local == sha256)
                        {
                            Log(mode, "Skipped (SHA256)");
                            return true;
                        }
                    }
                    break;
            }
        }
        return false;
    }
    public async Task DownloadOutlink(IEnumerable<IOutlinkHandler> outlinkHandlers, string content)
    {
        HashSet<string> usedLinks = [];
        foreach (IOutlinkHandler handler in outlinkHandlers)
        {
            try
            {
                Regex pattern = handler.Pattern;
                await handler.ProcessMatches(this, content, usedLinks,
                    pattern.Matches(content).Append(pattern.Match(Post.Post.Embed.URL ?? ""))).C();
            }
            catch (Exception ex)
            {
                Log(IOutlinkHandler.MODE, $"Exception: {ex.GetType()} {ex.Message}");
            }
        }
    }
}