using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

sealed partial class OneDriveOutlinkHandler : IOutlinkHandler, IDisposable
{
    private static readonly MediaTypeWithQualityHeaderValue Any = new("*/*");
    private static readonly MediaTypeWithQualityHeaderValue ApplicationJson = new(MediaTypeNames.Application.Json);
    [GeneratedRegex(@"(?<url>https://(?:1drv\.ms/[^""'<>\s]+|[^\.]+\.sharepoint\.com/[^""'<>\s]+))")]
    internal static partial Regex RegOneDrive();
    public static OneDriveOutlinkHandler Instance { get; } = new();

    private bool canceled;
    private SocketsHttpHandler? handler;
    private HttpClient? client;
    private HttpClient? Client
    {
        get
        {
            if (canceled || client is not null)
                return client;
            Console.WriteLine("AuthenticationHeader:");
            ReadOnlySpan<char> span = Console.ReadLine().AsSpan().Trim();
            if (span.IsEmpty)
            {
                canceled = true;
                return null;
            }
            handler = new()
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = false,
                UseProxy = true
            };
            client = new(handler);
            int space = span.IndexOf(' ');
            client.DefaultRequestHeaders.Authorization = space < 0 ?
                new(new(span)) :
                new(new(span[..space].Trim()), new(span[space..].Trim()));
            return client;
        }
    }
    public Regex Pattern => RegOneDrive();
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
            PostContext.Log(IOutlinkHandler.MODE, $"Find Outlink of OneDrive: {text}");
            int index = context.OutlinkCounter++;

            if (!PostContext.SkipDownloadIfAlreadyDone(path, IOutlinkHandler.MODE, Program.SaveModeOutlink))
            {
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
                continue;
            }
            Utils.SaveFile(text, fileName, context.PageFolderPath, context.Datetime, context.DatetimeEdited, Program.SaveModeOutlink);
            HttpClient? odClient = Client;
            if (odClient is null)
            {
                PostContext.Log(IOutlinkHandler.MODE, "AuthenticationHeader not set");
                return;
            }
            string sharingToken = EncodeSharingUrl(text);
            string endpoint = $"https://graph.microsoft.com/v1.0/shares/u!{sharingToken}/driveItem";
            PostContext.Log(IOutlinkHandler.MODE, $"Metadata {endpoint}");
            DriveItem? driveItem;
            string? raw = null;
            using (HttpRequestMessage reqMetadata = new(HttpMethod.Get, endpoint))
            {
                reqMetadata.Headers.Accept.Clear();
                reqMetadata.Headers.Accept.Add(ApplicationJson);
                using HttpResponseMessage respMetadata = await odClient.SendAsync(reqMetadata, HttpCompletionOption.ResponseContentRead).C();
                raw = await respMetadata.Content.ReadAsStringAsync().C();
                driveItem = await respMetadata.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.DriveItem).C();
            }
            if (driveItem is not { Name: not null, File: not null })
            {
                PostContext.Log(IOutlinkHandler.MODE, "Invalid driveItem");
                PostContext.Log(IOutlinkHandler.MODE, raw);
                continue;
            }
            if (context.PrepareDownload(
                driveItem.Name,
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
            Array256bit sha256 = new();
            if (driveItem.File.Hashes.SHA256Hash?.Length is SHA256.HashSizeInBits / 4)
            {
                Convert.FromHexString(driveItem.File.Hashes.SHA256Hash, sha256, out _, out _);
                if (context.SkipDownloadIfAlreadyDone(path, in sha256, IOutlinkHandler.MODE))
                    goto E;
            }
            string? url;
            using (HttpRequestMessage reqContent = new(HttpMethod.Get, $"{endpoint}/content"))
            {
                reqContent.Headers.Accept.Clear();
                reqContent.Headers.Accept.Add(Any);
                using HttpResponseMessage respContent = await odClient.SendAsync(reqContent, HttpCompletionOption.ResponseHeadersRead).C();
                url = respContent.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    PostContext.Log(IOutlinkHandler.MODE, $"Cannot get URL! Code: {respContent.StatusCode}");
                    continue;
                }
            }
            PostContext.Log(IOutlinkHandler.MODE, "aria2c!");
            Program.Aria2cDownload(context.PageFolderPath, fileName, url);
        E:
            if (driveItem.File.Hashes.SHA256Hash?.Length is SHA256.HashSizeInBits / 4)
                context.DownloadCache[sha256] = path;
            Utils.SetTime(path,
                driveItem.FileSystemInfo.CreatedDateTime ?? driveItem.CreatedDateTime ?? context.Datetime,
                driveItem.FileSystemInfo.LastModifiedDateTime ?? driveItem.LastModifiedDateTime ?? context.DatetimeEdited);
            if (fileNameNoExt is not null
                && pathNoExt is not null
                && !Directory.Exists(pathNoExt)
                && !File.Exists(pathNoExt)
                && PostContext.IsArchiveExtension(Path.GetExtension(path)))
                context.ArchiveParts.Add(pathNoExt, (path, null));
        }
    }

    public static string EncodeSharingUrl(string shareUrl)
    {
        const string table = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        if (string.IsNullOrEmpty(shareUrl))
            return string.Empty;
        int utf8Length = Encoding.UTF8.GetByteCount(shareUrl);
        int base64Length = (utf8Length * 4 + 2) / 3;
        Span<char> charBuffer = base64Length <= 1024 ? stackalloc char[base64Length] : new char[base64Length];
        int charIndex = 0;
        int byteCount = 0;
        Span<byte> byteBuffer = stackalloc byte[6];
        foreach (Rune rune in shareUrl.EnumerateRunes())
        {
            int count = rune.EncodeToUtf8(byteBuffer[byteCount..]);
            byteCount += count;
            if (byteCount >= 3)
            {
                int offset = 0;
                do
                {
                    int v0 = byteBuffer[offset++] << 16;
                    int v1 = byteBuffer[offset++] << 8;
                    int v2 = byteBuffer[offset++];
                    int value = v0 | v1 | v2;
                    charBuffer[charIndex++] = table[(value >> 18) & 0x3F];
                    charBuffer[charIndex++] = table[(value >> 12) & 0x3F];
                    charBuffer[charIndex++] = table[(value >> 6) & 0x3F];
                    charBuffer[charIndex++] = table[value & 0x3F];
                    byteCount -= 3;
                } while (byteCount >= 3);
                byteBuffer.Slice(offset, byteCount).CopyTo(byteBuffer);
            }
        }
        switch (byteCount)
        {
            case 2:
                int v1 = byteBuffer[0] << 16;
                int v2 = byteBuffer[1] << 8;
                int value = v1 | v2;
                charBuffer[charIndex++] = table[(value >> 18) & 0x3F];
                charBuffer[charIndex++] = table[(value >> 12) & 0x3F];
                charBuffer[charIndex++] = table[(value >> 6) & 0x3F];
                break;
            case 1:
                value = byteBuffer[0] << 16;
                charBuffer[charIndex++] = table[(value >> 18) & 0x3F];
                charBuffer[charIndex++] = table[(value >> 12) & 0x3F];
                break;
        }

        return new string(charBuffer);
    }

    public void Dispose()
    {
        client?.Dispose();
        handler?.Dispose();
        GC.SuppressFinalize(this);
    }

    internal sealed class DriveFile
    {
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("hashes")]
        public Hashes Hashes { get; set; }
    }

    internal struct FileSystemInfo
    {
        [JsonPropertyName("createdDateTime")]
        public DateTime? CreatedDateTime { get; set; }

        [JsonPropertyName("lastModifiedDateTime")]
        public DateTime? LastModifiedDateTime { get; set; }
    }

    internal struct Hashes
    {
        [JsonPropertyName("crc32Hash")]
        public string? CRC32Hash { get; set; }

        [JsonPropertyName("quickXorHash")]
        public string? QuickXorHash { get; set; }

        [JsonPropertyName("sha1Hash")]
        public string? SHA1Hash { get; set; }

        [JsonPropertyName("sha256Hash")]
        public string? SHA256Hash { get; set; }
    }

    internal sealed class DriveItem
    {
        [JsonPropertyName("createdDateTime")]
        public DateTime? CreatedDateTime { get; set; }

        [JsonPropertyName("lastModifiedDateTime")]
        public DateTime? LastModifiedDateTime { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("file")]
        public DriveFile? File { get; set; }

        [JsonPropertyName("fileSystemInfo")]
        public FileSystemInfo FileSystemInfo { get; set; }
    }
}
