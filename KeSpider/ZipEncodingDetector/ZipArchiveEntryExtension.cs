using System.IO.Compression;
using System.Runtime.CompilerServices;
using UtfUnknown;

namespace KeSpider.ZipEncodingDetector;

static class ZipArchiveExtension
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_storedEntryNameBytes")]
    private static extern ref byte[] GetStoredEntryNameBytes(this ZipArchiveEntry zipArchiveEntry);

    public static DetectionResult DetectEncoding(
        this IEnumerable<ZipArchiveEntry> entries,
        long? maxBytesToRead = null)
    {
        using ZipEntryNameStream stream = new(entries);
        return CharsetDetector.DetectFromStream(stream, maxBytesToRead);
    }

    public static DetectionResult DetectEncoding(
        this ZipArchive archive,
        long? maxBytesToRead = null)
        => archive.Entries.DetectEncoding(maxBytesToRead);

    public static byte[] GetRawEntryName(
        this ZipArchiveEntry entry)
        => entry.GetStoredEntryNameBytes();
}