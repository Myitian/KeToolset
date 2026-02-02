using System.Text;
using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

sealed partial class GoogleDriveOutlinkHandler : IOutlinkHandler
{
    [GeneratedRegex(@"(?<url>https://drive\.google\.com/(?:file/d|drive/folders)/[^""'<>\s]+)")]
    internal static partial Regex RegGoogleDrive();
    [GeneratedRegex(@"(?<url>https://drive\.google\.com/file/d/(?<id>[^""'<>\s?#/]+))")]
    internal static partial Regex RegGoogleDriveFile();
    public static GoogleDriveOutlinkHandler Instance { get; } = new();
    public Regex Pattern => RegGoogleDrive();
    public async ValueTask ProcessMatches(
        PostContext context,
        string content,
        HashSet<string> usedLinks,
        params IEnumerable<Match> matches)
    {
        HashSet<string> gDriveIDs = [];
        foreach (Match m in matches)
        {
            if (!m.Success)
                continue;
            string text = m.Groups["url"].Value;
            if (!usedLinks.Add(text))
                continue;
            string fileName = Utils.ReplaceInvalidFileNameChars(text) + ".placeholder.txt";
            string path = Path.Combine(context.PageFolderPath, fileName);
            Console.WriteLine($"    @O - Find Outlink of GoogleDrive: {text}");
            int index = context.OutlinkCounter++;

            if (Program.SaveModeContent == SaveMode.Skip && File.Exists(path))
            {
                PostContext.Log(IOutlinkHandler.MODE, "Skipped");
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
                continue;
            }
            Utils.SaveFile(text, fileName, context.PageFolderPath, context.Datetime, context.DatetimeEdited, Program.SaveModeOutlink);
            Match mm = RegGoogleDriveFile().Match(text);
            if (mm.Success)
            {
                string gdid = mm.Groups["id"].Value;
                if (gDriveIDs.Contains(gdid))
                    continue;
                string urlDirect = $"https://drive.usercontent.google.com/download?export=download&authuser=0&confirm=t&id={gdid}";
                using HttpRequestMessage headReq = new(HttpMethod.Head, urlDirect);
                using HttpResponseMessage headResp = await context.Client.SendAsync(headReq).C();
                if (headResp.Content.Headers.ContentDisposition?.FileName is not null)
                {
                    // In this API, GoogleDrive will send filename in "filename" with UTF-8 encoding, not Latin-1.
                    // And the "filename*" will not be provided.
                    headResp.Content.Headers.ContentDisposition.FileName = Encoding.UTF8.GetString(
                        Encoding.Latin1.GetBytes(headResp.Content.Headers.ContentDisposition.FileName));
                }
                string? name = headResp.Content.Headers.ContentDisposition?.FileNameStar
                            ?? headResp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                            ?? headResp.RequestMessage?.RequestUri?.AbsolutePath
                            ?? "file";
                if (context.PrepareDownload(
                    Path.GetFileName(name),
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
                if (!PostContext.SkipDownloadIfAlreadyDone(path, IOutlinkHandler.MODE))
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
                gDriveIDs.Add(gdid);
            }
        }
    }
}
