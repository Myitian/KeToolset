using System.Runtime.CompilerServices;

namespace KeSpider;

[InlineArray(32)]
struct Array256bit : IEquatable<Array256bit>
{
    private byte _;

    public Array256bit() { }
    public Array256bit(Span<byte> bytes)
    {
        bytes[..32].CopyTo(this);
    }

    public readonly bool Equals(Array256bit other)
        => ((ReadOnlySpan<byte>)this).SequenceEqual(other);
    public override readonly bool Equals(object? obj)
        => obj is Array256bit arr && Equals(arr);
    public override readonly int GetHashCode()
        => BitConverter.ToInt32(this);
    public static bool operator ==(Array256bit left, Array256bit right)
        => left.Equals(right);
    public static bool operator !=(Array256bit left, Array256bit right)
        => !(left == right);
}
