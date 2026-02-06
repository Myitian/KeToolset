using System.Net;
using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

sealed partial class MediafireOutlinkHandler : IOutlinkHandler
{

    [GeneratedRegex(@"(?<url>https://www\.mediafire\.com/(?:\?|file/)[a-zA-Z0-9]+)")]
    internal static partial Regex RegMediaFire();
    [GeneratedRegex(@"<a\s(?:[^>]*\s)?(?:href=""(?<url>[^""]+)""\s(?:[^>]*\s)?id=""downloadButton""|href=""(?<url>[^""]+)""\s(?:[^>]*\s)?id=""downloadButton"")")]
    internal static partial Regex RegMediafireFile();
    [GeneratedRegex(@"<div\s(?:[^>]*\s)?class=""filename""\s(?:[^>]*\s)?>(?<name>[^<]+)</div")]
    internal static partial Regex RegMediafireFileName();
    public static MediafireOutlinkHandler Instance { get; } = new();

    public Regex Pattern => RegMediaFire();
    public async ValueTask ProcessMatches(
        PostContext context,
        string content,
        HashSet<string> usedLinks,
        params IEnumerable<Match> matches)
    {
        foreach (Match m in matches)
        {
            if (!m.Success)
                continue;
            string text = m.Groups["url"].Value;
            if (!usedLinks.Add(text))
                continue;
            string fileName = Utils.ReplaceInvalidFileNameChars(text) + ".placeholder.txt";
            string path = Path.Combine(context.PageFolderPath, fileName);
            PostContext.Log(IOutlinkHandler.MODE, $"Find Outlink of Mediafire: {text}");
            int index = context.OutlinkCounter++;

            if (Program.SaveModeOutlink == SaveMode.Skip && File.Exists(path))
            {
                PostContext.Log(IOutlinkHandler.MODE, "Skipped");
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
                continue;
            }
            Utils.SaveFile(text, fileName, context.PageFolderPath, context.Datetime, context.DatetimeEdited, Program.SaveModeOutlink);
            string html = await context.Client.GetStringAsync(text).C();
            Match mm = RegMediafireFile().Match(html);
            if (mm.Success)
            {
                string urlDirect = WebUtility.UrlDecode(mm.Groups["url"].Value);
                string name = RegMediafireFileName().Match(html) is { Success: true } match ?
                    match.Groups["name"].Value : Path.GetFileName(urlDirect);
                if (context.PrepareDownload(
                    name,
                    index,
                    out fileName,
                    out path,
                    out string? fileNameNoExt,
                    out string? pathNoExt,
                    IOutlinkHandler.MODE,
                    "o"))
                {
                    PostContext.Log(IOutlinkHandler.MODE, "Pass Extracted Archive File!");
                    continue;
                }
                if (!PostContext.SkipDownloadIfAlreadyDone(path, IOutlinkHandler.MODE, Program.SaveModeOutlink))
                {
                    PostContext.Log(IOutlinkHandler.MODE, "aria2c!");
                    Program.Aria2cDownload(context.PageFolderPath, fileName, urlDirect);
                }
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
                if (fileNameNoExt is not null
                    && pathNoExt is not null
                    && !Directory.Exists(pathNoExt)
                    && !File.Exists(pathNoExt)
                    && Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".rar" or ".7z" or ".gz" or ".tar" or ".r00" or ".001")
                    context.ArchiveParts.Add(pathNoExt, (path, null));
            }
        }
    }
}
