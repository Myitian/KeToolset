using System.Buffers.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MegaDownloaderXmlGenerator;

public static class MegaUtils
{
    private static readonly HttpClient http = new();
    private static readonly ReadOnlyMemoryContent content = new("""[{"a":"f","c":1,"r":1}]"""u8.ToArray());

    public static bool ExtraerIdKey(ReadOnlyMemory<char> url, out ReadOnlyMemory<char> id, out ReadOnlyMemory<char> key)
    {
        id = default;
        key = default;
        int exclamation = url.Span.LastIndexOf('!');
        if (exclamation < 0)
        {
            int hashtag = url.Span.IndexOf('#');
            if (hashtag < 0)
                return false;
            key = url[(hashtag + 1)..];
            url = url[..hashtag];
            int slash = url.Span.LastIndexOf('/');
            if (slash < 0)
                return false;
            id = url[(slash + 1)..];
        }
        else
        {
            key = url[(exclamation + 1)..];
            url = url[..exclamation];
            int hashtag = url.Span.IndexOf('#');
            if (hashtag < 0)
                return false;
            id = url[(hashtag + 1)..];
            char[] newId = new char[id.Length];
            id.Span.Replace(newId, '!', '?');
            id = newId;
        }
        return true;
    }
    public static async Task<List<(string Url, string Path)>> ResolveFolderAsync(
        ReadOnlyMemory<char> folderId,
        ReadOnlyMemory<char> folderKey,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage resp = await http.PostAsync(
            $"https://g.api.mega.co.nz/cs?id={DateTime.UtcNow.Ticks}&n={folderId}",
            content,
            cancellationToken);
        using Aes aes = Aes.Create();
        aes.Key = Base64Url.DecodeFromChars(folderKey.Span);
        Dictionary<string, (string Name, string? Parent)> fileLinkList = [];
        List<(string Url, string Path)> result = [];
        await foreach (EntryList list in resp.Content.ReadFromJsonAsAsyncEnumerable(
            AppJsonSerializerContext.Default.EntryList,
            cancellationToken))
        {
            foreach (EntryMetadata item in list.F)
            {
                if (item.T is not (EntryType.File or EntryType.Folder)
                    || item.H is not string h
                    || item.K is not string k
                    || k.IndexOf(':') is not (>= 0 and int colon))
                    continue;
                byte[] eData = Base64Url.DecodeFromChars(k.AsSpan(colon + 1));
                byte[] dData = aes.DecryptEcb(eData, PaddingMode.Zeros);
                Span<Int128> i128view = MemoryMarshal.Cast<byte, Int128>(dData.AsSpan());
                Int128 key = i128view.Length switch
                {
                    0 => 0, // should not happen!
                    1 => i128view[0],
                    _ => i128view[0] ^ i128view[1],
                };
                string text = DecryptEntryInfo(item.A, MemoryMarshal.AsBytes(new ReadOnlySpan<Int128>(in key)));
                EntryName name = JsonSerializer.Deserialize(text.AsSpan(4), AppJsonSerializerContext.Default.EntryName);
                fileLinkList[h] = (name.N ?? "", item.P);
                if (item.T is EntryType.File)
                    result.Add(($"http://mega.co.nz/#N!{h}!{Base64Url.EncodeToString(dData)}=###n={folderId}", h));
            }
        }
        for (int i = 0; i < result.Count; i++)
        {
            (string url, string id) = result[i];
            string? path = FlatRelations(id, fileLinkList);
            Debug.Assert(path is not null);
            result[i] = (url, path);
        }
        return result;
    }
    private static string DecryptEntryInfo(ReadOnlySpan<char> cipertext, ReadOnlySpan<byte> key)
    {
        using Aes aes = Aes.Create();
        aes.SetKey(key);
        // Yes, MEGA cloud is using an IV of all zeros for AES-CBC!
        ReadOnlySpan<byte> plaintext = aes.DecryptCbc(
            Base64Url.DecodeFromChars(cipertext),
            "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0"u8,
            PaddingMode.Zeros);
        return Encoding.UTF8.GetString(plaintext.TrimEnd((byte)0));
    }
    private static string? FlatRelations(string id, Dictionary<string, (string Name, string? Parent)> fileLinkList)
    {
        ref (string Name, string? Parent) reference = ref CollectionsMarshal.GetValueRefOrNullRef(fileLinkList, id);
        if (Unsafe.IsNullRef(in reference))
            return null;
        if (reference.Parent is null)
            return reference.Name;
        if (FlatRelations(reference.Parent, fileLinkList) is string path)
        {
            path = $"{path}/{reference.Name}";
            reference = (path, null);
            return path;
        }
        reference = (reference.Name, null);
        return reference.Name;
    }
    public enum EntryType
    {
        File,
        Folder
    }
    public struct EntryName
    {
        public string? N { get; set; }
    }
    public struct EntryMetadata
    {
        public string? H { get; set; }
        public string? P { get; set; }
        public EntryType T { get; set; }
        public string? A { get; set; }
        public string? K { get; set; }
    }
    public struct EntryList
    {
        public ImmutableArray<EntryMetadata> F { get; set; }
    }
}