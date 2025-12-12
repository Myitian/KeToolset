using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FFmpegCheck;

public readonly partial struct FileID(ulong volumeIndex, UInt128 fileIndex) : IEquatable<FileID>
{
    public static FileID Invalid => new(ulong.MaxValue, UInt128.MaxValue);
    public readonly ulong VolumeIndex = volumeIndex;
    public readonly UInt128 FileIndex = fileIndex;
    public readonly bool IsInvalid => VolumeIndex == ulong.MaxValue && FileIndex == UInt128.MaxValue;
    public override readonly string ToString() => $"<{VolumeIndex:X8}@{FileIndex:X16}>";

    public static FileID GetFromFile(string file)
    {
        if (!OperatingSystem.IsWindows())
            return Stat.GetFileID(file); // call SystemNative_Stat (in native part in .NET runtime)
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 2)) // Windows 8+
            return FILE_ID_INFO.GetFileID(file); // ReFS use 128-bit ID.
        return BY_HANDLE_FILE_INFORMATION.GetFileID(file); // Other Windows
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
    private readonly partial struct BY_HANDLE_FILE_INFORMATION
    {
        private readonly uint _A; // useless in this project
        private readonly uint _B; // useless in this project
        private readonly uint _C; // useless in this project
        private readonly uint _D; // useless in this project
        private readonly uint _E; // useless in this project
        private readonly uint _F; // useless in this project
        private readonly uint _G; // useless in this project
        private readonly uint VolumeSerialNumber;
        private readonly uint _I; // useless in this project
        private readonly uint _J; // useless in this project
        private readonly uint _K; // useless in this project
        private readonly uint FileIndexHigh;
        private readonly uint FileIndexLow;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        internal static FileID GetFileID(string file)
        {
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandle(fs.SafeFileHandle, out BY_HANDLE_FILE_INFORMATION info))
                        return new(info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
            return Invalid;
        }
    }

    [SupportedOSPlatform("windows6.2")] // windows6.2 = Windows 8
    [StructLayout(LayoutKind.Sequential)]
    private readonly partial struct FILE_ID_INFO
    {
        private readonly ulong VolumeSerialNumber;
        private readonly ulong FileIdLow;
        private readonly ulong FileIdHigh;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandleEx(
            SafeFileHandle hFile,
            int FileInformationClass,
            out FILE_ID_INFO lpFileInformation,
            int dwBufferSize);

        internal static FileID GetFileID(string file)
        {
            const int FileIdInfo = 18;
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandleEx(fs.SafeFileHandle, FileIdInfo, out FILE_ID_INFO info, Unsafe.SizeOf<FILE_ID_INFO>()))
                        return new(info.VolumeSerialNumber, new(info.FileIdHigh, info.FileIdLow));
            return Invalid;
        }
    }

    // compatible with .NET 7+, see
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Unix/System.Native/Interop.Stat.cs
    // https://github.com/dotnet/runtime/blob/main/src/native/libs/System.Native/pal_io.h
    [UnsupportedOSPlatform("windows")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly partial struct Stat
    {
        private readonly int _A; // useless in this project
        private readonly int _B; // useless in this project
        private readonly uint _C; // useless in this project
        private readonly uint _D; // useless in this project
        private readonly long _E; // useless in this project
        private readonly long _F; // useless in this project
        private readonly long _G; // useless in this project
        private readonly long _H; // useless in this project
        private readonly long _I; // useless in this project
        private readonly long _J; // useless in this project
        private readonly long _K; // useless in this project
        private readonly long _L; // useless in this project
        private readonly long _M; // useless in this project
        private readonly long Dev;
        private readonly long _O; // useless in this project
        private readonly long Ino;
        private readonly uint _Q; // useless in this project

        [LibraryImport("libSystem.Native", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
        private static partial int SystemNative_Stat(string path, out Stat output);

        internal static FileID GetFileID(string file)
        {
            if (SystemNative_Stat(file, out Stat stat) == 0)
                return new((ulong)stat.Dev, (ulong)stat.Ino);
            return Invalid;
        }
    }
}