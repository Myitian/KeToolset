using KeSpider.API;
using KeSpider.OutlinkHandlers;
using KeSpider.ZipEncodingDetector;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UtfUnknown;

namespace KeSpider;

class Program
{
    readonly static HashSet<IOutlinkHandler> outlinkHandlers = [];
    readonly static string proxy = "127.0.0.1:10809";
    readonly static string aria2cFile = "aria2c";
    readonly static string _7zFile = "7z";

    static Program()
    {
        bool a2c = false;
        bool _7z = false;
        ReadOnlySpan<char> path = Environment.GetEnvironmentVariable("Path");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (ReadOnlySpan<char> item in new WindowsEnvironmentPathEnumerator(path))
            {
                if (!a2c)
                {
                    string exePath = Path.Combine(item.ToString(), "aria2c.exe");
                    if (File.Exists(exePath))
                    {
                        aria2cFile = exePath;
                        a2c = true;
                    }
                }
                if (!_7z)
                {
                    string exePath = Path.Combine(item.ToString(), "7z.exe");
                    if (File.Exists(exePath))
                    {
                        _7zFile = exePath;
                        _7z = true;
                    }
                }
                if (a2c && _7z)
                    break;
            }
        }
        else
        {
            foreach (Range range in path.Split(Path.PathSeparator))
            {
                ReadOnlySpan<char> item = path[range].Trim();
                if (item.IsEmpty)
                    continue;
                if (!a2c)
                {
                    string exePath = Path.Combine(item.ToString(), "aria2c");
                    if (File.Exists(exePath))
                    {
                        aria2cFile = exePath;
                        a2c = true;
                    }
                }
                if (!_7z)
                {
                    string exePath = Path.Combine(item.ToString(), "7z");
                    if (File.Exists(exePath))
                    {
                        _7zFile = exePath;
                        _7z = true;
                    }
                }
                if (a2c && _7z)
                    break;
            }
        }
        outlinkHandlers.Add(GoogleDriveOutlinkHandler.Instance);
        outlinkHandlers.Add(OneDriveOutlinkHandler.Instance);
        outlinkHandlers.Add(MediafireOutlinkHandler.Instance);
        outlinkHandlers.Add(new SimpleOutlinkHandler("Mega", Regexes.RegMega(), ""));
        outlinkHandlers.Add(new SimpleOutlinkHandler("BaiduPan", Regexes.RegBaiduPan(), "?pwd="));
    }

    public static string FixSpecialExt(string name)
    {
        if (!SpecialExtFix)
            return name;
        var lookup = SpecialExts.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> ext = Path.GetExtension(name.AsSpan());
        if (!lookup.TryGetValue(ext, out string? newExt))
            return name;
        return string.Concat(name.AsSpan(0, name.Length - ext.Length), newExt);
    }

    public static void Aria2cDownload(string folder, string name, string url, params IEnumerable<string> headers)
    {
        ProcessStartInfo aria2c = new(aria2cFile, [
            $"--all-proxy={proxy}",
            "--console-log-level=error",
            "--auto-file-renaming=false",
            "--summary-interval=0",
            "--allow-overwrite=true","--check-certificate=false",
            ..headers.Select(it => $"--header={it}"),
            "-s", "16",
            "-x", "16",
            "-k", "1M",
            "-d", folder,
            "-o", name,
            url]);
        Process.Start(aria2c)?.WaitForExit();
    }

    public static void SevenZipExtract(string folder, string path, string? password = null)
    {
        ProcessStartInfo _7z = new(_7zFile, [
            "x",
            $"-o{folder}",
            .. password is not null ? [$"-p{password}"] : Enumerable.Empty<string>(),
            path]);
        Process.Start(_7z)?.WaitForExit();
    }

    static readonly Regex rXXX = Regexes.RegMultiPartNumberOnly();
    static readonly Regex rPartXRar = Regexes.RegMultiPartRar();
    static readonly Regex rRxx = Regexes.RegMultiPartRxx();
    static readonly Regex rZxx = Regexes.RegMultiPartZxx();

    public static Dictionary<string, string> SpecialExts = new()
    {
        { ".7", ".7z" },
        { ".zi", ".zip" }
    };
    public static bool SpecialExtFix { get; private set; } = true;

    public static bool DL_Json { get; private set; } = true;
    public static bool DL_File { get; private set; } = true;
    public static bool DL_Content { get; private set; } = true;
    public static bool DL_Outlink { get; private set; } = true;
    public static bool DL_Picture { get; private set; } = true;
    public static SaveMode SavemodeJson { get; private set; } = SaveMode.Replace;
    public static SaveMode SavemodeFile { get; private set; } = SaveMode.Replace;
    public static SaveMode SavemodeContent { get; private set; } = SaveMode.Replace;
    public static SaveMode SavemodeOutlink { get; private set; } = SaveMode.Replace;
    public static SaveMode SavemodePicture { get; private set; } = SaveMode.Replace;

    static async Task Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        using SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            UseProxy = true
        };
        HttpClient client = new(handler);
        client.DefaultRequestHeaders.Accept.Add(new("text/css"));
        Console.WriteLine("Use aria2c: " + aria2cFile);
        Console.WriteLine("Use 7z: " + _7zFile);
        Console.WriteLine("Use proxy: " + proxy);

        Console.WriteLine("Mode [0:All/1:Selected]?:");
        bool all = Console.ReadLine()?.Trim() != "1";
        HashSet<PostInfo> posts = all ? await LoadAllPosts(client) : LoadSelectedPosts();
        Console.WriteLine();
        Console.WriteLine("------");
        Console.WriteLine("Enter Settings:");
        Console.WriteLine("OverridingEncoding:");
        string? encName = Console.ReadLine();
        Encoding? encoding = GetEncoding(encName);

        Console.WriteLine();
        Console.WriteLine("------");
        Console.WriteLine("Current Settings:");

        Console.WriteLine("OverridingEncoding: " + (encoding?.EncodingName ?? "null"));
        Console.WriteLine("dl_json: " + DL_Json);
        Console.WriteLine("dl_file: " + DL_File);
        Console.WriteLine("dl_content: " + DL_Content);
        Console.WriteLine("dl_outlink: " + DL_Outlink);
        Console.WriteLine("dl_picture: " + DL_Picture);
        Console.WriteLine("savemode_json: " + SavemodeJson);
        Console.WriteLine("savemode_file: " + SavemodeFile);
        Console.WriteLine("savemode_content: " + SavemodeContent);
        Console.WriteLine("savemode_outlink: " + SavemodeOutlink);
        Console.WriteLine("savemode_picture: " + SavemodePicture);

        Console.WriteLine();
        Console.WriteLine("------");
        Console.WriteLine("Enter Destination Folder:");
        string destination = Path.GetFullPath(Console.ReadLine() ?? "./");

        Console.WriteLine();
        Console.WriteLine("------");
        Console.WriteLine("# Searching in URLs...");

        Dictionary<Array256bit, string> dlCache = [];
        Directory.CreateDirectory(destination);

        int i = 0;
        foreach ((string id, string user, string service, string domain) in posts)
            i = await ProcessPost(client, posts, encoding, destination, dlCache, i, id, user, service, domain);

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Done!");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit the program.");
        Console.ReadKey();
    }

    private static Encoding? GetEncoding(string? encName)
    {
        if (int.TryParse(encName, out int codepage))
        {
            try
            {
                return Encoding.GetEncoding(codepage);
            }
            catch { }
        }
        if (!string.IsNullOrWhiteSpace(encName))
        {
            try
            {
                return Encoding.GetEncoding(encName);
            }
            catch { }
        }
        return null;
    }

    private static HashSet<PostInfo> LoadSelectedPosts()
    {
        Regex rPostPage = Regexes.RegPostPage();
        HashSet<PostInfo> posts = [];
        Console.WriteLine("Enter Post Links:");
        Match mm;
        while (true)
        {
            string url = Console.ReadLine() ?? "";
            mm = rPostPage.Match(url);
            if (!mm.Success)
                break;
            string domain = mm.Groups["domain"].Value;
            string service = mm.Groups["service"].Value;
            string user = mm.Groups["user"].Value;
            string id = mm.Groups["id"].Value;
            posts.Add(new(id, user, service, domain));
        }
        return posts;
    }

    private static async Task<HashSet<PostInfo>> LoadAllPosts(HttpClient client)
    {
        Regex rMainPage = Regexes.RegMainPage();
        HashSet<PostInfo> posts = [];
        Match mm;
        do
        {
            Console.WriteLine("Enter Author's Page URL:");
            string url = Console.ReadLine() ?? "";
            mm = rMainPage.Match(url);
        }
        while (!mm.Success);
        string domain = mm.Groups["domain"].Value;
        string service = mm.Groups["service"].Value;
        string user = mm.Groups["user"].Value;
        Console.WriteLine("Enter Min ID:");
        string? minIDStr = Console.ReadLine();
        long? minID = null;
        if (long.TryParse(minIDStr, out long minIDV))
            minID = minIDV;
        Console.WriteLine("Enter Max ID:");
        string? maxIDStr = Console.ReadLine();
        long? maxID = null;
        if (long.TryParse(maxIDStr, out long maxIDV))
            maxID = maxIDV;
        Console.WriteLine("Enter Name Filter (Regex):");
        string? userRegexStr = Console.ReadLine();
        Regex? userRegex = string.IsNullOrEmpty(userRegexStr) ? null : new(userRegexStr);
        int offeet = 0;
        List<PostsResult>? postsAPIResult;
        do
        {
            postsAPIResult = await PostsResult.Request(client, domain, service, user, offeet);
            offeet += 50;
            if (postsAPIResult?.Count is null or <= 0)
                break;
            foreach (PostsResult post in postsAPIResult)
            {
                if (long.TryParse(post.ID, out long postID))
                {
                    if (minID.HasValue && postID < minID)
                        continue;
                    if (maxID.HasValue && postID > maxID)
                        continue;
                }
                else
                {
                    if (!string.IsNullOrEmpty(minIDStr) && post.ID.CompareTo(minIDStr) < 0)
                        continue;
                    if (!string.IsNullOrEmpty(maxIDStr) && post.ID.CompareTo(maxIDStr) > 0)
                        continue;
                }
                if (!(userRegex?.IsMatch(post.Title) ?? true))
                    continue;
                posts.Add(new(post, domain));
            }
        }
        while (postsAPIResult?.Count >= 50);
        return posts;
    }

    private static async Task<int> ProcessPost(
        HttpClient client,
        HashSet<PostInfo> posts,
        Encoding? encoding,
        string destination,
        Dictionary<Array256bit, string> dlCache,
        int i,
        string id,
        string user,
        string service,
        string domain)
    {
        dlCache.Clear();
        try
        {
            Console.WriteLine($"  * {i * 100.0 / posts.Count,5:f2}% - {++i} of {posts.Count}");
            (byte[] json, PostRoot? post) = await PostRoot.Request(client, domain, service, user, id);
            if (post is null)
            {
                Console.WriteLine($"Failed to get post https://{domain}/api/v1/{service}/user/{user}/post/{post}");
                goto END;
            }
            ReadOnlySpan<char> rawName = Utils.XTrim(post.Post.Title);
            string pagename = Utils.ReplaceInvalidFileNameChars(rawName);
            DateTime datetime = Utils.NormalizeTime(post.Post.Published).ToLocalTime();
            DateTime datetimeEdited = Utils.NormalizeTime(post.Post.Edited ?? post.Post.Published).ToLocalTime();
            string pagenameWithID = $"{id}_{pagename}";
            string pageFolderPath = Path.Combine(destination, pagenameWithID);

            if (Directory.EnumerateDirectories(destination, $"{id}_*").FirstOrDefault() is string s
                && Path.GetFileName(s) != pagenameWithID)
            {
                Console.WriteLine($"  !! Renaming old directory: {s}");
                Console.WriteLine($"  !! To: {pageFolderPath}");
                Directory.Move(s, pageFolderPath);
            }
            Console.WriteLine($"  >> {id} {rawName}");
            DirectoryInfo dir = Directory.CreateDirectory(pageFolderPath);
            pageFolderPath = dir.FullName;

            if (DL_Json)
            {
                Console.WriteLine("    @J - Save JSON");
                Utils.SaveFile(json, "post.json", pageFolderPath, datetime, datetimeEdited, SavemodeJson);
            }
            if (DL_File)
            {
                await DownloadFile(client, encoding, dlCache, domain, post, datetime, datetimeEdited, pageFolderPath);
            }

            string? content = null;
            if (DL_Content || DL_Outlink)
                content = post.Post.Content;

            if (DL_Content && content != null)
            {
                Console.WriteLine("    @C - Save Content");

                string path = Path.Combine(pageFolderPath, "content.html");
                if (SavemodeContent == SaveMode.Skip && File.Exists(path))
                {
                    Console.WriteLine("    @C - Skipped");
                    Utils.SetTime(path, datetime, datetimeEdited);
                }
                else
                    Utils.SaveFile(content, "content.html", pageFolderPath, datetime, datetimeEdited, SavemodeContent);
            }

            if (DL_Outlink && content != null)
            {
                await DownloadOutlink(client, dlCache, post, datetime, datetimeEdited, pageFolderPath, content);
            }

            if (DL_Picture)
            {
                DownloadPicture(dlCache, post, datetime, datetimeEdited, pageFolderPath);
            }

            Utils.SetTime(pageFolderPath, datetime, datetimeEdited);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    END:
        return i;
    }


    private static async Task DownloadFile(
        HttpClient client,
        Encoding? encoding,
        Dictionary<Array256bit, string> dlCache,
        string domain,
        PostRoot post,
        DateTime datetime,
        DateTime datetimeEdited,
        string pageFolderPath)
    {
        if (post?.Attachments?.Count is null or 0)
            return;
        int j = 0, fid = 0;
        Dictionary<string, (string Path, string? Password)> part1s = [];
        foreach (Attachment file in post.Attachments)
        {
            try
            {
                string fileUrl = $"{file.Server}/data{file.Path}";
                Console.WriteLine($"    @F - {++j} of {post.Attachments.Count} - Download File {fileUrl}");
                ReadOnlySpan<char> name = string.IsNullOrEmpty(file.Name) || file.Name.StartsWith("https://www.patreon.com/media-u/", StringComparison.Ordinal)
                    ? Path.GetFileName((ReadOnlySpan<char>)file.Path) : file.Name;
                string fileNameMain = Utils.ReplaceInvalidFileNameChars(name);
                string fileName = FixSpecialExt(fileNameMain);
                string oldName = $"{fid}_{fileName}";
                string newName = $"{fid++.ToString().PadLeft(3, '0')}_{fileName}";
                int mLen = 0, num = 0;
                do
                {
                    if (Path.GetExtension(fileName) is ".zip" or ".rar" or ".7z" or ".gz" or ".tar")
                    {
                        mLen = -1;
                        break;
                    }
                    Match m = rXXX.Match(fileName);
                    if (m.Success && int.TryParse(m.Groups["num"].Value, out num))
                    {
                        mLen = m.Length;
                        break;
                    }
                    m = rPartXRar.Match(fileName);
                    if (m.Success && int.TryParse(m.Groups["num"].Value, out num))
                    {
                        mLen = m.Length;
                        break;
                    }
                    m = rRxx.Match(fileName);
                    if (m.Success && int.TryParse(m.Groups["num"].Value, out num))
                    {
                        mLen = m.Length;
                        break;
                    }
                    m = rZxx.Match(fileName);
                    if (m.Success && int.TryParse(m.Groups["num"].Value, out num))
                    {
                        mLen = m.Length;
                        break;
                    }
                    fileName = newName;
                } while (false);
                string path = Path.Combine(pageFolderPath, fileName);
                if (fileName != oldName)
                {
                    string oldPath = Path.Combine(pageFolderPath, oldName);
                    if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, path);
                        Console.WriteLine("fix old name!");
                    }
                }
                if (fileName != newName)
                {
                    string newPath = Path.Combine(pageFolderPath, newName);
                    if (File.Exists(newPath))
                    {
                        File.Move(newPath, path);
                        Console.WriteLine("fix old name!");
                    }
                }
                string namePart = mLen > 0 ? fileName[..^mLen] : Path.GetFileNameWithoutExtension(fileName);
                string d = Path.Combine(pageFolderPath, namePart);
                if (mLen != 0 && Directory.Exists(d))
                {
                    Console.WriteLine("    @F - Pass Extracted Archive File!");
                    continue;
                }
                Array256bit sha256url = new();
                Convert.FromHexString(file.Path.AsSpan(7, 64), sha256url, out _, out _);
                if (dlCache.TryGetValue(sha256url, out string? duplicated))
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    Console.WriteLine("    @F - Link");
                    Utils.MakeLink(path, duplicated);
                    goto E;
                }
                else if (File.Exists(path))
                {
                    switch (SavemodeFile)
                    {
                        case SaveMode.Skip:
                            Console.WriteLine($"    @F - Skipped");
                            goto E;
                        case SaveMode.Replace:
                            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                Array256bit sha256local = new();
                                SHA256.HashData(fs, sha256local);
                                if (sha256local == sha256url)
                                {
                                    Console.WriteLine($"    @F - Skipped (SHA256)");
                                    goto E;
                                }
                            }
                            break;
                    }
                }
                Console.WriteLine($"    @F - aria2c!");
                Aria2cDownload(pageFolderPath, fileName, fileUrl);
            E:
                dlCache[sha256url] = fileName;
                Utils.SetTime(path, datetime, datetimeEdited);
                FileInfo fi = new(path);
                string oldA = Path.Combine(pageFolderPath, namePart);
                string oldB = Path.Combine(pageFolderPath, namePart);
                bool dNEx = !Directory.Exists(d) && !File.Exists(d);
                if (dNEx)
                {
                    if (Directory.Exists(oldA))
                        Directory.Move(oldA, d);
                    else if (Directory.Exists(oldB))
                        Directory.Move(oldB, d);
                    else if (fi.Extension is ".zip" or ".rar" or ".7z" or ".gz" or ".tar" or ".r00" or ".001")
                    {
                        Archive archive = await Archive.Request(client, domain, file.Stem);
                        string? password = archive.Password;
                        part1s.Add(d, (path, password));
                    }
                }
            }
            catch { }
        }
        foreach ((string p1, (string path, string? password)) in part1s)
        {
            if (!Directory.Exists(p1) && !File.Exists(p1))
            {
                string? pwd = password;
                switch (pwd)
                {
                    case null when path.EndsWith(".zip"):
                        try
                        {
                            Console.WriteLine("    @F - zip! Trying to use .NET built-in API...");
                            using ZipArchive zip0 = ZipFile.OpenRead(path);
                            Console.WriteLine("    @F - zip! Detecting encoding...");
                            DetectionResult result = zip0.DetectEncoding();
                            DetectionDetail? detected = result.Detected;
                            foreach (DetectionDetail detail in result.Details)
                                Console.WriteLine($"    @F - zip! CONF:{detail.Confidence} E:{detail.EncodingName}");
                            Encoding u;
                            switch (detected?.EncodingName)
                            {
                                case "utf-8":
                                case "ascii":
                                case "gb18030" when detected.Confidence > 0.7f:
                                case "big5" when detected.Confidence > 0.7f:
                                case "shift_jis" when detected.Confidence > 0.7f:
                                    Console.WriteLine($"    @F - zip! Result: {(u = detected.Encoding).EncodingName}");
                                    break;
                                default:
                                    if (encoding is null)
                                        throw new NotSupportedException();
                                    Console.WriteLine($"    @F - zip! OverridenBy: {(u = encoding).EncodingName}");
                                    break;
                            }
                            ZipFile.ExtractToDirectory(path, p1, u, true);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    @F - zip! Exception: {ex.Message}");
                            goto default;
                        }
                    case "":
                        Console.WriteLine("Need password!");
                        pwd = Console.ReadLine();
                        goto default;
                    default:
                        if (pwd is null)
                            Console.WriteLine($"    @F - 7z! \"{p1}\" \"{path}\"");
                        else
                            Console.WriteLine($"    @F - 7z! \"{p1}\" \"{path}\" pwd\"{pwd}\"");
                        SevenZipExtract(p1, path, pwd);
                        break;
                }
            }
        }
    }

    private static async Task DownloadOutlink(
        HttpClient client,
        Dictionary<Array256bit, string> dlCache,
        PostRoot post,
        DateTime datetime,
        DateTime datetimeEdited,
        string pageFolderPath,
        string content)
    {
        HashSet<string> usedLinks = [];
        foreach (IOutlinkHandler handler in outlinkHandlers)
        {
            try
            {
                Regex pattern = handler.Pattern;
                await handler.ProcessMatches(client, dlCache, post, datetime, datetimeEdited, pageFolderPath, content, usedLinks,
                    pattern.Matches(content).Append(pattern.Match(post.Post.Embed.URL ?? "")));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    private static void DownloadPicture(
        in Dictionary<Array256bit, string> dlCache,
        in PostRoot post,
        in DateTime datetime,
        in DateTime datetimeEdited,
        in string pageFolderPath)
    {
        if (post?.Previews?.Count is null or 0)
            return;
        int j = 0;
        foreach (Attachment picture in post.Previews)
        {
            if (string.IsNullOrEmpty(picture.Path) || string.IsNullOrEmpty(picture.Server))
                continue;
            try
            {
                string fileUrl = $"{picture.Server}/data{picture.Path}";
                Console.WriteLine($"    @P - {j + 1} of {post.Previews.Count} - Download Picture {fileUrl}");
                ReadOnlySpan<char> name = string.IsNullOrEmpty(picture.Name) || picture.Name.StartsWith("https://www.patreon.com/media-u/", StringComparison.Ordinal)
                    ? Path.GetFileName(picture.Path.AsSpan()) : picture.Name;
                string validName = Utils.ReplaceInvalidFileNameChars(name);
                string ext = Path.GetExtension(validName);
                string prefix = j++.ToString().PadLeft(3, '0');
                string fileName = $"{prefix}_{validName}";
                string path = Path.Combine(pageFolderPath, fileName);
                string[] oldNames = [
                    .. Directory.EnumerateFiles(pageFolderPath, $"{j}_*{ext}"),
                    .. Directory.EnumerateFiles(pageFolderPath, $"{prefix}_*{ext}").Where(it => it != path)
                ];
                if (oldNames.Length > 0)
                {
                    File.Move(oldNames[0], path);
                    Console.WriteLine("fix old name!");
                    for (int ii = 1; ii < oldNames.Length; ii++)
                    {
                        Console.WriteLine("Delete extra old name!");
                        File.Delete(oldNames[ii]);
                    }
                }
                Array256bit sha256url = new();
                Convert.FromHexString(picture.Path.AsSpan(7, 64), sha256url, out _, out _);
                if (dlCache.TryGetValue(sha256url, out string? duplicated))
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    Console.WriteLine("    @P - Link");
                    Utils.MakeLink(path, duplicated);
                    goto E;
                }
                else if (File.Exists(path))
                {
                    switch (SavemodePicture)
                    {
                        case SaveMode.Skip:
                            Console.WriteLine("    @P - Skipped");
                            goto E;
                        case SaveMode.Replace:
                            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                Array256bit sha256local = new();
                                SHA256.HashData(fs, sha256local);
                                if (sha256local == sha256url)
                                {
                                    Console.WriteLine("    @P - Skipped (SHA256)");
                                    goto E;
                                }
                            }
                            break;
                    }
                }
                Aria2cDownload(pageFolderPath, fileName, fileUrl);
            E:
                dlCache[sha256url] = fileName;
                Utils.SetTime(path, datetime, datetimeEdited);
            }
            catch { }
        }
    }
}