namespace MegaDownloaderTaskReset;

static class FieldComparers
{
    public static readonly ChildNumberValueComparer<int> PriorityAsc = new("Prioridad");
    public static readonly DescComparer<XmlNode> PriorityDesc = new(PriorityAsc);
    public static readonly ChildNumberValueComparer<long> SizeAsc = new("TamanoBytes");
    public static readonly DescComparer<XmlNode> SizeDesc = new(SizeAsc);
    public static readonly ChildNumberValueComparer<decimal> PercentAsc = new("Porcentaje");
    public static readonly DescComparer<XmlNode> PercentDesc = new(PercentAsc);

    public static readonly ChildStringValueComparer PackageNameAsc = new("Nombre");
    public static readonly DescComparer<XmlNode> PackageNameDesc = new(PackageNameAsc);
    public static readonly ChildStringValueComparer PackageLocalPathAsc = new("RutaLocal");
    public static readonly DescComparer<XmlNode> PackageLocalPathDesc = new(PackageLocalPathAsc);

    public static readonly ChildStringValueComparer FileNameAsc = new("NombreFichero");
    public static readonly DescComparer<XmlNode> FileNameDesc = new(FileNameAsc);
    public static readonly ChildStringValue2Comparer FileLocalPathAsc = new("RutaLocal", "NombreFichero");
    public static readonly DescComparer<XmlNode> FileLocalPathDesc = new(FileLocalPathAsc);
    public static readonly ChildStringValue2Comparer FileRelativePathAsc = new("RutaRelativa", "NombreFichero");
    public static readonly DescComparer<XmlNode> FileRelativePathDesc = new(FileRelativePathAsc);
    public static readonly ChildStringValueWithPreprocessorComparer FileUrlAsc = new("URL", Program.DecryptString);
    public static readonly DescComparer<XmlNode> FileUrlDesc = new(FileUrlAsc);

    internal sealed class DescComparer<T>(IComparer<T> comparer) : IComparer<T>
    {
        public int Compare(T? x, T? y)
        {
            return comparer.Compare(y, x);
        }
    }
    internal sealed class ChildStringValueComparer(string name) : IComparer<XmlNode>
    {
        public int Compare(XmlNode? x, XmlNode? y)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(
                x?[name]?.Value,
                y?[name]?.Value);
        }
    }
    internal sealed class ChildStringValue2Comparer(string name1, string name2) : IComparer<XmlNode>
    {
        public int Compare(XmlNode? x, XmlNode? y)
        {
            int i = StringComparer.OrdinalIgnoreCase.Compare(
                x?[name1]?.Value,
                y?[name1]?.Value);
            return i != 0 ? i : StringComparer.OrdinalIgnoreCase.Compare(
                x?[name2]?.Value,
                y?[name2]?.Value);
        }
    }
    internal sealed class ChildStringValueWithPreprocessorComparer(string name, Func<string, string> preprocessor) : IComparer<XmlNode>
    {
        public int Compare(XmlNode? x, XmlNode? y)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(
                preprocessor.InvokeWhenNotNull(x?[name]?.Value),
                preprocessor.InvokeWhenNotNull(y?[name]?.Value));
        }
    }
    internal sealed class ChildNumberValueComparer<T>(string name) : IComparer<XmlNode>
        where T : struct, IParsable<T>
    {
        public int Compare(XmlNode? x, XmlNode? y)
        {
            return Nullable.Compare<T>(
                T.TryParse(x?[name]?.Value, null, out T xn) ? xn : null,
                T.TryParse(y?[name]?.Value, null, out T yn) ? yn : null);
        }
    }
    static TResult? InvokeWhenNotNull<T, TResult>(this Func<T, TResult> func, T? arg)
    {
        if (arg is null)
            return default;
        return func.Invoke(arg);
    }
}