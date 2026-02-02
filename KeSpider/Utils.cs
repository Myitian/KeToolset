using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace KeSpider;

static class Utils
{
    readonly static FrozenSet<char> InvalidFileNameChars = Path.GetInvalidFileNameChars().ToFrozenSet();
    internal static ConfiguredTaskAwaitable C(this Task task)
        => task.ConfigureAwait(false);
    internal static ConfiguredTaskAwaitable<TResult> C<TResult>(this Task<TResult> task)
        => task.ConfigureAwait(false);
    internal static ConfiguredValueTaskAwaitable C(this ValueTask task)
        => task.ConfigureAwait(false);
    internal static ConfiguredValueTaskAwaitable<TResult> C<TResult>(this ValueTask<TResult> task)
        => task.ConfigureAwait(false);
    public static void MakeLink(string fileName, string existingFileName)
    {
        if (!HardLink.Create(fileName, existingFileName))
            File.CreateSymbolicLink(fileName, existingFileName);
    }
    public static DateTime NormalizeTime(DateTime time)
    {
        if (time.Kind is not DateTimeKind.Unspecified)
            return time;
        return DateTime.SpecifyKind(time, DateTimeKind.Utc);
    }
    public static string ReplaceInvalidFileNameChars(ReadOnlySpan<char> source)
    {
        Span<char> span = stackalloc char[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            span[i] = InvalidFileNameChars.Contains(c) ? '_' : c;
        }
        return span.ToString();
    }
    public static void SaveFile(string content, string fileName, string pageFolderPath, DateTime createTime, DateTime? editTime = null, SaveMode savemode = SaveMode.Replace)
        => SaveFile(Encoding.UTF8.GetBytes(content), fileName, pageFolderPath, createTime, editTime, savemode);
    public static void SaveFile(ReadOnlySpan<byte> content, string fileName, string pageFolderPath, DateTime createTime, DateTime? editTime = null, SaveMode savemode = SaveMode.Replace)
    {
        string path = Path.Combine(pageFolderPath, fileName);
        if (File.Exists(path))
        {
            switch (savemode)
            {
                case SaveMode.Replace:
                    break;
                case SaveMode.Skip:
                    return;
                case SaveMode.KeepBoth:
                    int i = 0;
                    while (File.Exists(path))
                    {
                        path = $"{Path.GetFileNameWithoutExtension(fileName)}_({i})";
                        string ext = Path.GetExtension(fileName);
                        if (!string.IsNullOrEmpty(ext))
                            path += "." + ext;
                        path = Path.Combine(pageFolderPath, path);
                    }
                    break;

            }
        }
        File.WriteAllBytes(path, content);
        SetTime(path, createTime, editTime);
    }
    public static void SetTime(string path, DateTime createTime, DateTime? editTime)
    {
        DateTime editTimeNotNull = NormalizeTime(editTime ?? createTime);
        createTime = NormalizeTime(createTime);
        FileSystemInfo? fsi = File.Exists(path) ? new FileInfo(path) : Directory.Exists(path) ? new DirectoryInfo(path) : null;
        if (fsi?.Exists is true)
        {
            fsi.CreationTime = createTime;
            fsi.LastAccessTime = editTimeNotNull;
            fsi.LastWriteTime = editTimeNotNull;
        }
    }
    public static ReadOnlySpan<char> XTrim(ReadOnlySpan<char> text)
    {
        int i = 0, j = text.Length - 1;
        while (i <= j)
        {
            if (char.GetUnicodeCategory(text[i])
                is UnicodeCategory.SpaceSeparator
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.Format
                or UnicodeCategory.Control)
                i++;
            else if (char.GetUnicodeCategory(text[j])
                is UnicodeCategory.SpaceSeparator
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.Format
                or UnicodeCategory.Control)
                j--;
            else break;
        }
        return text[i..(j + 1)];
    }
    public static IEnumerable<T> AsEnumerable<T>(params IEnumerable<T> values) => values;
}
