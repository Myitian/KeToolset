using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

sealed partial class GoogleDriveOutlinkHandler : IOutlinkHandler
{
    [GeneratedRegex(@"https://drive\.google\.com/(?:file/d|drive/folders)/[^""'<>\s]+")]
    internal static partial Regex RegGoogleDrive();
    [GeneratedRegex(@"(?<=https://drive\.google\.com/file/d/)[^""'<>\s?#/&]+")]
    internal static partial Regex RegGoogleDriveFile();
    [GeneratedRegex(@"(?<=name=""at""\s+value="")[^""]+")]
    internal static partial Regex RegGoogleDriveIntermediatePageAt();
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
            string text = m.Value;
            if (!usedLinks.Add(text))
                continue;
            string fileName = Utils.ReplaceInvalidFileNameChars(text) + ".placeholder.txt";
            string path = Path.Combine(context.PageFolderPath, fileName);
            PostContext.Log(IOutlinkHandler.MODE, $"Find Outlink of GoogleDrive: {text}");
            int index = context.OutlinkCounter++;

            if (Program.SaveModeContent == SaveMode.Skip && File.Exists(path))
            {
                PostContext.Log(IOutlinkHandler.MODE, "Skipped");
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
                continue;
            }
            Utils.SaveFile(text, fileName, context.PageFolderPath, context.Datetime, context.DatetimeEdited, Program.SaveModeOutlink);
            if (!RegGoogleDriveFile().ValueMatch(text, out ValueMatch mm))
                continue;
            ReadOnlySpan<char> gdid = text.AsSpan(mm.Index, mm.Length);
            if (gDriveIDs.GetAlternateLookup<ReadOnlySpan<char>>().Contains(gdid))
                continue;
            char[] gdidOnHeap = gdid.ToArray();
            string? name;
            string urlDirect = $"https://drive.usercontent.google.com/download?export=download&authuser=0&confirm=t&id={gdid}";
            using (HttpRequestMessage pageReq = new(HttpMethod.Get, urlDirect) { Headers = { Range = new(0, 0) } })
            using (HttpResponseMessage pageResp = await context.Client.SendAsync(pageReq, HttpCompletionOption.ResponseHeadersRead).C())
            {
                if (!pageResp.IsSuccessStatusCode)
                {
                    PostContext.Log(IOutlinkHandler.MODE, $"Status code {(int)pageResp.StatusCode} {pageResp.StatusCode}");
                    continue;
                }
                ContentDispositionHeaderValue? cd = null;
                if (pageResp.Content.Headers.ContentDisposition is not null)
                    cd = pageResp.Content.Headers.ContentDisposition;
                else
                {
                    // the intermediate page is triggered
                    ReadOnlySpan<char> body = await pageResp.Content.ReadAsStringAsync().C();
                    if (RegGoogleDriveIntermediatePageAt().ValueMatch(body, out mm))
                    {
                        ReadOnlySpan<char> at = text.AsSpan(mm.Index, mm.Length);
                        urlDirect = $"{urlDirect}&at={at}";
                        using HttpRequestMessage headReq = new(HttpMethod.Head, urlDirect);
                        using HttpResponseMessage headResp = await context.Client.SendAsync(headReq).C();
                        if (!headResp.IsSuccessStatusCode)
                        {
                            PostContext.Log(IOutlinkHandler.MODE, $"Status code {(int)pageResp.StatusCode} {pageResp.StatusCode}");
                            continue;
                        }
                        cd = headResp.Content.Headers.ContentDisposition;
                    }
                    else
                    {
                        PostContext.Log(IOutlinkHandler.MODE, "Failed to get file from GoogleDrive:");
                        PostContext.Log(IOutlinkHandler.MODE, body);
                        continue;
                    }
                }
                if (cd?.FileName is not null)
                {
                    // In this API, GoogleDrive will send filename in "filename" with UTF-8 encoding, not Latin-1.
                    // And the "filename*" will not be provided.
                    cd.FileName = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(cd.FileName));
                }
                name = cd?.FileNameStar
                    ?? cd?.FileName?.Trim('"')
                    ?? "file";
            }
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
            if (!PostContext.SkipDownloadIfAlreadyDone(path, IOutlinkHandler.MODE, Program.SaveModeOutlink)
                || File.Exists($"{path}.aria2"))
            {
                PostContext.Log(IOutlinkHandler.MODE, "aria2c!");
                Program.Aria2cDownload(context.PageFolderPath, fileName, urlDirect);
            }
            Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
            if (fileNameNoExt is not null
                && pathNoExt is not null
                && !Directory.Exists(pathNoExt)
                && !File.Exists(pathNoExt)
                && PostContext.IsArchiveExtension(Path.GetExtension(path)))
                context.ArchiveParts.Add(pathNoExt, (path, null));
            gDriveIDs.GetAlternateLookup<ReadOnlySpan<char>>().Add(gdidOnHeap);
        }
    }
}
