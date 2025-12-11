using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: DisableRuntimeMarshalling]

namespace FFmpegCheck;

public partial struct FileID(ulong volumeIndex, UInt128 fileIndex) : IEquatable<FileID>
{
    public static FileID Invalid => new(ulong.MaxValue, UInt128.MaxValue);
    public ulong VolumeIndex = volumeIndex;
    public UInt128 FileIndex = fileIndex;

    public static FileID GetFromFile(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2)) // Windows 8
                return FILE_ID_INFO.GetFileID(file); // ReFS use 128-bit ID.
            return BY_HANDLE_FILE_INFORMATION.GetFileID(file);
        }
        // may support other systems in the future.
        // e.g. stat()
        return Invalid;
    }

    public readonly bool Equals(FileID other)
        => VolumeIndex == other.VolumeIndex && FileIndex == other.FileIndex;
    public override readonly bool Equals(object? obj)
        => obj is FileID other && Equals(other);
    public override readonly int GetHashCode()
        => HashCode.Combine(VolumeIndex, FileIndex);
    public static bool operator ==(FileID left, FileID right)
        => left.Equals(right);
    public static bool operator !=(FileID left, FileID right)
        => !left.Equals(right);

    [SupportedOSPlatform("windows")]
    [StructLayout(LayoutKind.Sequential)]
    private partial struct BY_HANDLE_FILE_INFORMATION
    {
        public uint _0;
        public uint _1;
        public uint _2;
        public uint _3;
        public uint _4;
        public uint _5;
        public uint _6;
        public uint VolumeSerialNumber;
        public uint _8;
        public uint _9;
        public uint _A;
        public uint FileIndexHigh;
        public uint FileIndexLow;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        public static FileID GetFileID(string file)
        {
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandle(fs.SafeFileHandle, out BY_HANDLE_FILE_INFORMATION info))
                        return new(info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
            return Invalid;
        }
    }

    [SupportedOSPlatform("windows6.2")]
    [StructLayout(LayoutKind.Sequential)]
    private partial struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandleEx(
            SafeFileHandle hFile,
            int FileInformationClass,
            out FILE_ID_INFO lpFileInformation,
            int dwBufferSize);

        public static FileID GetFileID(string file)
        {
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandleEx(fs.SafeFileHandle, 18, out FILE_ID_INFO info, Unsafe.SizeOf<FILE_ID_INFO>()))
                        return new(info.VolumeSerialNumber, new(info.FileIdHigh, info.FileIdLow));
            return Invalid;
        }
    }
}