using System.Buffers;
using System.Collections;

namespace KeSpider;

/// <summary>
/// Parsing of the Path environment variable.<br/>The parsing results are basically the same as those implemented by Windows itself.
/// </summary>
ref struct WindowsEnvironmentPathEnumerator(ReadOnlySpan<char> chars) : IEnumerator<ReadOnlySpan<char>>
{
    private readonly static SearchValues<char> specialChars = SearchValues.Create(";\"");
    private readonly ReadOnlySpan<char> span = chars;
    private int i = 0;
    private int start = 0;
    private bool quoted = false;
    private bool parsed = false;
    private bool finished = false;
    readonly object IEnumerator.Current => throw new NotSupportedException();
    public ReadOnlySpan<char> Current { readonly get; private set; }
    public readonly WindowsEnvironmentPathEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        if (finished)
            return false;
        ReadOnlySpan<char> sub;
        int next;
        while (i < span.Length)
        {
            switch (span[i])
            {
                case ';' when !quoted:
                    if (parsed)
                    {
                        parsed = false;
                        start = ++i;
                    }
                    else
                    {
                        sub = span[start..i].Trim();
                        start = ++i;
                        if (!sub.IsEmpty)
                        {
                            Current = sub;
                            return true;
                        }
                    }
                    break;
                case '"' when quoted:
                    sub = span[start..i].Trim();
                    next = span[i..].IndexOf(';');
                    if (next == -1)
                        i = span.Length;
                    else
                        i += next;
                    parsed = true;
                    quoted = false;
                    if (!sub.IsEmpty)
                    {
                        Current = sub;
                        return true;
                    }
                    break;
                case '"':
                    quoted = true;
                    start = ++i;
                    break;
                default:
                    next = quoted ? span[i..].IndexOf('"') : span[i..].IndexOfAny(specialChars);
                    if (next == -1)
                        i = span.Length;
                    else
                        i += next;
                    break;
            }
        }
        finished = true;
        sub = span[start..].Trim();
        if (!sub.IsEmpty)
        {
            Current = sub;
            return true;
        }
        return false;
    }

    public void Reset()
    {
        i = 0;
        start = 0;
        quoted = false;
        parsed = false;
        finished = false;
        Current = [];
    }

    public readonly void Dispose()
    {
    }
}
