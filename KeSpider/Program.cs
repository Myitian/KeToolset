using KeCore;
using KeCore.API;
using KeSpider.OutlinkHandlers;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace KeSpider;

static class Program
{
    static readonly HashSet<IOutlinkHandler> outlinkHandlers = [];
    static readonly string proxy = "127.0.0.1:10809";
    static readonly string aria2cFile = "aria2c";
    static readonly string _7zFile = "7z";
    internal static readonly Regex rXXX = Regexes.RegMultiPartNumberOnly();
    internal static readonly Regex rPartXRar = Regexes.RegMultiPartRar();
    internal static readonly Regex rRxx = Regexes.RegMultiPartRxx();
    internal static readonly Regex rZxx = Regexes.RegMultiPartZxx();
    internal static readonly EnumerationOptions simpleNonRecursiveEnumeration = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        MatchCasing = MatchCasing.CaseInsensitive,
        MatchType = MatchType.Simple,
    };

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
        name = name.TrimEnd('.');
        if (!SpecialExtFix)
            return name;
        var lookup = SpecialExts.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> ext = Path.GetExtension(name.AsSpan());
        if (!lookup.TryGetValue(ext, out string? newExt))
            return name;
        return string.Concat(name.AsSpan(0, name.Length - ext.Length), newExt);
    }
    public static int ProcessArchiveName(string fileName)
    {
        int extLen = 0;
        ReadOnlySpan<char> ext = Path.GetExtension(fileName.AsSpan());
        Span<char> extLC = stackalloc char[ext.Length];
        ext.ToLowerInvariant(extLC);
        if (extLC is ".zip" or ".rar" or ".7z" or ".gz" or ".tar")
            return ext.Length;
        Match m = rXXX.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["num"].ValueSpan, out _))
            return m.Length;
        m = rPartXRar.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["num"].ValueSpan, out _))
            return m.Length;
        m = rRxx.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["num"].ValueSpan, out _))
            return m.Length;
        m = rZxx.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["num"].ValueSpan, out _))
            return m.Length;
        return extLen;
    }
    public static void Aria2cDownload(string folder, string name, string url, params IEnumerable<string> headers)
    {
        ProcessStartInfo aria2c = new(aria2cFile, [
            $"--all-proxy={proxy}",
            "--console-log-level=error",
            "--auto-file-renaming=false",
            "--summary-interval=0",
            "--max-tries=50",
            "--max-file-not-found=10",
            "--lowest-speed-limit=128",
            "--allow-overwrite=true",
            "--check-certificate=false",
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
    public static Dictionary<string, string> SpecialExts = new()
    {
        { ".7", ".7z" },
        { ".zi", ".zip" }
    };
    public static bool SpecialExtFix { get; private set; } = true;
    public static bool DownloadJson { get; private set; } = true;
    public static bool DownloadFile { get; private set; } = true;
    public static bool DownloadContent { get; private set; } = true;
    public static bool DownloadOutlink { get; private set; } = true;
    public static bool DownloadPicture { get; private set; } = true;
    public static SaveMode SaveModeJson { get; private set; } = SaveMode.Replace;
    public static SaveMode SaveModeFile { get; private set; } = SaveMode.Replace;
    public static SaveMode SaveModeContent { get; private set; } = SaveMode.Replace;
    public static SaveMode SaveModeOutlink { get; private set; } = SaveMode.Replace;
    public static SaveMode SaveModePicture { get; private set; } = SaveMode.Replace;

    static async Task Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;
        using SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            UseProxy = true
        };
        using HttpClient client = new(handler);
        client.DefaultRequestHeaders.Accept.Add(new("text/css")); // the website's strange firewall rule for API requests
        AssemblyName assembly = Assembly.GetExecutingAssembly().GetName();
        Console.WriteLine($"{assembly.Name} v{assembly.Version}");
        Console.WriteLine($"Use aria2c: {aria2cFile}");
        Console.WriteLine($"Use 7z: {_7zFile}");
        Console.WriteLine($"Use proxy: {proxy}");

        Console.WriteLine("Mode [0:All/1:Selected]?:");
        bool all = Console.ReadLine()?.Trim() != "1";
        HashSet<PostInfo> posts = all ? await LoadAllPosts(client).C() : LoadSelectedPosts();
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
        Console.WriteLine("dl_json: " + DownloadJson);
        Console.WriteLine("dl_file: " + DownloadFile);
        Console.WriteLine("dl_content: " + DownloadContent);
        Console.WriteLine("dl_outlink: " + DownloadOutlink);
        Console.WriteLine("dl_picture: " + DownloadPicture);
        Console.WriteLine("savemode_json: " + SaveModeJson);
        Console.WriteLine("savemode_file: " + SaveModeFile);
        Console.WriteLine("savemode_content: " + SaveModeContent);
        Console.WriteLine("savemode_outlink: " + SaveModeOutlink);
        Console.WriteLine("savemode_picture: " + SaveModePicture);

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
        foreach (PostInfo post in posts)
            i = await ProcessPost(client, posts, encoding, destination, dlCache, i, post).C();

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
        return await KeCoreUtils.LoadAllPosts(client, domain, service, user, Predicate).C();

        bool Predicate(PostsResult post)
        {
            if (long.TryParse(post.ID, out long postID))
            {
                if (minID.HasValue && postID < minID)
                    return false;
                if (maxID.HasValue && postID > maxID)
                    return false;
            }
            else
            {
                if (!string.IsNullOrEmpty(minIDStr) && post.ID.CompareTo(minIDStr) < 0)
                    return false;
                if (!string.IsNullOrEmpty(maxIDStr) && post.ID.CompareTo(maxIDStr) > 0)
                    return false;
            }
            return userRegex?.IsMatch(post.Title) ?? true;
        }
    }

    private static async Task<int> ProcessPost(
        HttpClient client,
        HashSet<PostInfo> posts,
        Encoding? encoding,
        string destination,
        Dictionary<Array256bit, string> dlCache,
        int i,
        PostInfo post)
    {
        try
        {
            Console.WriteLine($"  * {i * 100.0 / posts.Count,5:f2}% - {++i} of {posts.Count}");
            (byte[] json, PostRoot? postRoot) = await PostRoot.Request(client, post.Domain, post.Service, post.User, post.ID).C();
            if (postRoot is null)
            {
                Console.WriteLine($"Failed to get post https://{post.Domain}/api/v1/{post.Service}/user/{post.User}/post/{postRoot}");
                goto END;
            }
            ReadOnlySpan<char> rawName = Utils.XTrim(postRoot.Post.Title);
            string pagename = Utils.ReplaceInvalidFileNameChars(rawName.TrimEnd().TrimEnd('.'));
            DateTime datetime = Utils.NormalizeTime(postRoot.Post.Published).ToLocalTime();
            DateTime datetimeEdited = Utils.NormalizeTime(postRoot.Post.Edited ?? postRoot.Post.Published).ToLocalTime();
            string pagenameWithID = $"{post.ID}_{pagename}";
            string pageFolderPath = Path.Combine(destination, pagenameWithID);

            if (Directory.EnumerateDirectories(destination, $"{post.ID}_*").FirstOrDefault() is string s
                && Path.GetFileName(s) != pagenameWithID)
            {
                Console.WriteLine($"  !! Renaming old directory: {s}");
                Console.WriteLine($"  !! To: {pageFolderPath}");
                Directory.Move(s, pageFolderPath);
            }
            Console.WriteLine($"  >> {post.ID} {rawName}");
            DirectoryInfo dir = Directory.CreateDirectory(pageFolderPath);
            PostContext context = new(client, encoding, dlCache, postRoot, datetime, datetimeEdited, pageFolderPath);

            string? content = null;
            if (DownloadContent || DownloadOutlink)
                content = postRoot.Post.Content;

            if (DownloadJson)
                context.DownloadJson(json);
            if (DownloadContent && content != null)
                context.DownloadContent(content);
            if (DownloadOutlink && content != null)
                await context.DownloadOutlink(outlinkHandlers, content).C();
            if (DownloadPicture)
                context.DownloadPicture();
            if (DownloadFile)
                await context.DownloadFile(post.Domain).C();

            context.ProcessArchives();
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
}