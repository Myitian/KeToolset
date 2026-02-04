using Microsoft.VisualBasic.FileIO;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using static MegaDownloaderTaskReset.ConColor;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace MegaDownloaderTaskReset;

static class Program
{
    delegate bool CommandHandler(ref bool updateNotSave, ref XmlNode xml, ReadOnlySpan<string> args);
    static readonly KeyValuePair<string, string> v2 = new("v", "2");
    static readonly string xmlPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
         @"MegaDownloader\Config\DownloadList.xml");
    static readonly Dictionary<string, CommandHandler> commandRegistry = new(StringComparer.OrdinalIgnoreCase);
    static Program()
    {
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            if (updateNotSave)
            {
                Console.WriteLine("有未保存的修改！是否保存？");
                if (ReadBoolean())
                    WriteXML(xmlPath, xml);
            }
            Console.WriteLine("是否确认退出？");
            return ReadBoolean();
        }, "quit", "q", "exit");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            WriteLine(Console.Out,
                $"""
                 {WhiteFG}帮助：{Reset}
                 命令      | 参数     | 别名             | 说明
                 quit      : 无       : q, exit          : 退出
                 help      : 无       : h, ?             : 帮助
                 version   : 无       : v, ver           : 版本
                 dedup     : 检测方式 : dd               : 尝试文件去重
                 find      : 查询内容 : f, query, search : 查询
                 fix       : 无       : fx               : 修复目录和下载状态
                 info      : 无       : i, information   : 基本信息
                 list      : 无       : l                : 文件列表
                 reload    : 无       : r                : 重新加载
                 priority  : 无       : p                : 按照当前顺序重新设置优先级
                 reset     : 无       : rst              : 重置下载状态
                 save      : 无       : s                : 保存
                 save-as   : 文件路径 : sa, saveas       : 另存为
                 sort-file : 排序字段 : sf               : 重排文件
                 sort-pkg  : 排序字段 : sp               : 重排包
                 """);
            return false;
        }, "help", "h", "?");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            AssemblyName name = Assembly.GetExecutingAssembly().GetName();
            WriteLine(Console.Out,
                $"{WhiteFG}{name.Name} {CyanFG}v{name.Version}{Reset}");
            return false;
        }, "version", "v", "ver");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            if (args.IsEmpty)
            {
                WriteLine(Console.Out,
                    $"""
                    选择去重模式（idkey比较保守；keyonly可以发现更多潜在的重复文件）：
                    arg1{DarkGrayFG}:{Reset}[{WhiteFG}keyonly{DarkGrayFG}/{WhiteFG}idkey{Reset}]
                    arg2{DarkGrayFG}:{Reset}[{WhiteFG}copy{DarkGrayFG}/{WhiteFG}hardlink{DarkGrayFG}/{WhiteFG}symlink{DarkGrayFG}/{WhiteFG}removerecord{DarkGrayFG}/{WhiteFG}nop{Reset}]
                    """);
                args = ParseCommand(Console.ReadLine());
            }
            args.First2(out ReadOnlySpan<char> arg1, out ReadOnlySpan<char> arg2);
            Dictionary<string, List<(XmlNode, XmlNode)>> map = [];
            Dictionary<string, List<(XmlNode, XmlNode)>>.AlternateLookup<ReadOnlySpan<char>> lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files)
                    continue;
                foreach (XmlNode file in files.ChildNodes)
                {
                    if (file["URL"] is not XmlNode urlNode
                        || file["NombreFichero"] is null
                        || file["RutaLocal"] is null)
                        continue;
                    ReadOnlySpan<char> span = DecryptString(urlNode.Value);
                    switch (arg1)
                    {
                        case "keyonly":
                            int exclamation = span.LastIndexOf('!');
                            if (exclamation < 0)
                            {
                                int hashtag = span.IndexOf('#');
                                if (hashtag < 0)
                                    continue;
                                span = span[(hashtag + 1)..];
                            }
                            else
                            {
                                int equalsign = span.IndexOf('=');
                                if (equalsign < 0)
                                    continue;
                                span = span[(exclamation + 1)..equalsign];
                            }
                            lookup.AddItemOrInitList(span, (file, package));
                            break;
                        case "idkey":
                            exclamation = span.IndexOf('!');
                            if (exclamation < 0)
                            {
                                int slash = span.LastIndexOf('/');
                                if (slash < 0)
                                    continue;
                                span = span[(slash + 1)..];
                                lookup.AddItemOrInitList(span, (file, package));
                            }
                            else
                            {
                                int equalsign = span.IndexOf('=');
                                if (equalsign < 0)
                                    continue;
                                span = span[(exclamation + 1)..equalsign];
                                StackAllocWrapper(lookup, span, (file, package));
                                static void StackAllocWrapper(
                                    Dictionary<string, List<(XmlNode, XmlNode)>>.AlternateLookup<ReadOnlySpan<char>> lookup,
                                    ReadOnlySpan<char> src,
                                    (XmlNode, XmlNode) pair)
                                {
                                    Span<char> span = stackalloc char[src.Length];
                                    src.Replace(span, '!', '#');
                                    lookup.AddItemOrInitList(span, pair);
                                }
                            }
                            break;
                        default:
                            Console.WriteLine("参数无效");
                            return false;
                    }
                }
            }
            HashSet<XmlNode> duplicated = [];
            Dictionary<XmlNode, List<XmlNode>> pending = [];
            foreach ((string id, List<(XmlNode, XmlNode)> nodes) in map)
            {
                if (nodes.Count < 2)
                    continue;
                Span<(XmlNode File, XmlNode)> span = CollectionsMarshal.AsSpan(nodes);
                using (IMemoryOwner<(XmlNode, XmlNode)> mem = MemoryPool<(XmlNode, XmlNode)>.Shared.Rent(span.Length))
                    span.MergeSort(mem.Memory.Span, DeDupComparer.Instance);
                bool anyComplete = DeDupComparer.IsCompleted(span[0].File);
                duplicated.EnsureCapacity(duplicated.Count + nodes.Count - 1);
                WriteLine(Console.Out,
                    $"ID {BlueFG}{id}{Reset}");
                XmlNode? first = null;
                XmlNode? lastPkg = null;
                foreach ((XmlNode file, XmlNode package) in span)
                {
                    if (file["NombreFichero"] is not XmlNode fileName)
                        continue;
                    bool isComplete = DeDupComparer.IsCompleted(file);
                    ConColor qCol, tCol;
                    if (first is null)
                    {
                        qCol = DarkBlueFG;
                        tCol = BlueFG;
                        first = file;
                    }
                    else
                    {
                        qCol = DarkYellowFG;
                        tCol = YellowFG;
                        duplicated.Add(file);
                        if (anyComplete)
                            pending.AddItemOrInitList(first, file);
                    }
                    if (package != lastPkg)
                    {
                        WriteLine(Console.Out,
                            $"  - {WhiteFG}包 {qCol}\"{tCol}{package["Nombre"]?.Value}{qCol}\"{DarkGrayFG}：{Reset}");
                        lastPkg = package;
                    }
                    string relativeFileName = Path.Combine(file["RutaRelativa"]?.Value ?? "", fileName.Value);
                    WriteLine(Console.Out,
                        $"    - {WhiteFG}文件 {qCol}\"{tCol}{relativeFileName}{qCol}\"{DarkGrayFG}：{Reset}[{(isComplete ? GreenFG : RedFG)}{(isComplete ? "已完成" : "未完成")}{Reset}]");
                    string localFileName = Path.Combine(file["RutaLocal"]?.Value ?? "", fileName.Value);
                    WriteLine(Console.Out,
                        $"      {DarkGrayFG}{localFileName}{Reset}");
                }
            }
            WriteLine(Console.Out,
                $"{CyanFG}{duplicated.Count}{Reset}个记录待处理……");
            switch (arg2)
            {
                case "nop":
                    return false;
                case "removerecord":
                    foreach (XmlNode package in xml.ChildNodes)
                    {
                        if (package["ListaFicheros"] is not XmlNode files)
                            continue;
                        files.ChildNodes.RemoveAll(duplicated.Contains);
                    }
                    updateNotSave = true;
                    return false;
            }
            foreach ((XmlNode source, List<XmlNode> destinations) in pending)
            {
                if (source["NombreFichero"] is not XmlNode srcName
                    || source["RutaLocal"] is not XmlNode srcLocalPath)
                    continue;
                string srcFullPath = Path.Combine(srcLocalPath.Value, srcName.Value);
                string? totalBytes = source["TamanoBytes"]?.Value;
                string? bytesDownloaded = source["BytesDescargados"]?.Value;
                updateNotSave = true;
                foreach (XmlNode dst in destinations)
                {
                    if (dst["NombreFichero"] is not XmlNode dstName
                        || dst["RutaLocal"] is not XmlNode dstLocalPath)
                        continue;
                    if (Uri.TryCreate(dstName.Value, UriKind.Absolute, out Uri? uri) && uri.Scheme.StartsWith("http"))
                    {
                        WriteLine(Console.Out,
                            $"""
                            发现未解析的文件 {DarkYellowFG}"{YellowFG}{dstName.Value}{DarkYellowFG}"{Reset}
                            修复为 {DarkYellowFG}"{YellowFG}{srcName.Value}{DarkYellowFG}"{Reset}
                            """);
                        dstName.SetValue(srcName.Value);
                    }
                    string dstFullPath = Path.Combine(dstLocalPath.Value, dstName.Value);
                    WriteLine(Console.Out,
                        $"""
                        {DarkBlueFG}"{BlueFG}{srcFullPath}{DarkBlueFG}"{Reset}
                        => {DarkYellowFG}"{YellowFG}{dstFullPath}{DarkYellowFG}"{Reset}
                        """);
                    switch (arg2)
                    {
                        case "copy":
                            try
                            {
                                File.Copy(srcFullPath, dstFullPath, true);
                            }
                            catch (Exception ex)
                            {
                                Write(Console.Out,
                                    $"""
                                    {RedFG}复制时出现异常{DarkRedFG}：{RedFG}{ex}{Reset}
                                    是否继续？
                                    """);
                                if (!ReadBoolean())
                                    return false;
                            }
                            break;
                        case "hardlink":
                            try
                            {
                                File.Delete(dstFullPath);
                                if (!HardLink.Create(dstFullPath, srcFullPath))
                                {
                                    int errno = Marshal.GetLastPInvokeError();
                                    Write(Console.Out,
                                        $"""
                                        {RedFG}创建硬链接失败{DarkRedFG}：{RedFG}{errno} {Marshal.GetPInvokeErrorMessage(errno)}{Reset}
                                        是否继续？
                                        """);
                                    if (!ReadBoolean())
                                        return false;
                                }
                            }
                            catch (Exception ex)
                            {
                                Write(Console.Out,
                                    $"""
                                    {RedFG}创建硬链接时出现异常{DarkRedFG}：{RedFG}{ex}{Reset}
                                    是否继续？
                                    """);
                                if (!ReadBoolean())
                                    return false;
                            }
                            break;
                        case "symlink":
                            try
                            {
                                File.Delete(dstFullPath);
                                File.CreateSymbolicLink(dstFullPath, srcFullPath);
                            }
                            catch (Exception ex)
                            {
                                Write(Console.Out,
                                    $"""
                                    {RedFG}创建符号链接时出现异常{DarkRedFG}：{RedFG}{ex}{Reset}
                                    是否继续？
                                    """);
                                if (!ReadBoolean())
                                    return false;
                            }
                            break;
                        default:
                            Console.WriteLine("参数无效");
                            return false;
                    }
                    if (totalBytes is not null)
                        dst.GetChildOrAddNew("TamanoBytes").SetValue(totalBytes);
                    if (bytesDownloaded is not null)
                        dst.GetChildOrAddNew("BytesDescargados").SetValue(bytesDownloaded);
                    dst.GetChildOrAddNew("NumeroConexionesAbiertas").SetValue("0");
                    dst.GetChildOrAddNew("NumeroChunksAsignados").SetValue("0");
                    dst.GetChildOrAddNew("Porcentaje").SetValue("100");
                    dst.GetChildOrAddNew("DescargaComenzada").SetValue("True");
                    dst.GetChildOrAddNew("DescargaProcesada").SetValue("True");
                    dst.GetChildOrAddNew("ExtraccionFicheroAutomatica").SetValue("False");
                    dst.GetChildOrAddNew("VelocidadKBs").SetValue("0");
                    dst.GetChildOrAddNew("EstadoDescarga").SetValue("Completado");
                    dst.GetChildOrAddNew("MarcadoParaBorrarFicheroLocal").SetValue("False");
                    dst.GetChildOrAddNew("TiempoEstimadoDescarga").SetValue("");
                    dst.GetChildOrAddNew("PausaIndividual").SetValue("False");
                    dst.GetChildOrAddNew("DescargaIndividual").SetValue("False");
                    dst.GetChildOrAddNew("LimiteVelocidad").SetValue("0");
                    dst.GetChildOrAddNew("DatosPartes").SetValue("");
                }
            }
            return false;
        }, "dedup", "dd");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            ReadOnlySpan<char> arg1;
            if (!args.IsEmpty)
                arg1 = args[0];
            else
            {
                Console.WriteLine("输入搜索内容：");
                arg1 = Console.ReadLine().AsSpan().Trim().Trim('"');
            }
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files || package["Nombre"] is not XmlNode packageName)
                    continue;
                bool first = true;
                foreach (XmlNode file in files.ChildNodes)
                {
                    if (file["URL"] is not XmlNode url
                        || file["NombreFichero"] is not XmlNode fileName)
                        continue;
                    string relativeFileName = Path.Combine(file["RutaRelativa"]?.Value ?? "", fileName.Value);
                    string localFileName = Path.Combine(file["RutaLocal"]?.Value ?? "", fileName.Value);
                    if (!relativeFileName.AsSpan().Contains(arg1, StringComparison.CurrentCultureIgnoreCase)
                        && !localFileName.AsSpan().Contains(arg1, StringComparison.CurrentCultureIgnoreCase)
                        && !url.Value.AsSpan().Contains(arg1, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                    if (first)
                    {
                        WriteLine(Console.Out,
                            $"- {WhiteFG}包 {DarkYellowFG}\"{YellowFG}{packageName.Value}{DarkYellowFG}\"{DarkGrayFG}：{Reset}");
                        first = false;
                    }
                    WriteLine(Console.Out,
                        $"  - {WhiteFG}文件 {DarkYellowFG}\"{YellowFG}{relativeFileName}{DarkYellowFG}\"{DarkGrayFG}：{Reset}");
                    WriteLine(Console.Out,
                        $"    > {WhiteFG}URL{DarkGrayFG}: {GreenFG}{DecryptString(url.Value)}{Reset}");
                    if (file["Porcentaje"] is XmlNode percent)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}Porcentaje{DarkGrayFG}: {CyanFG}{percent.Value}{Reset}");
                    if (file["EstadoDescarga"] is XmlNode est)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}EstadoDescarga{DarkGrayFG}: {CyanFG}{est.Value}{Reset}");
                }
            }
            return false;
        }, "find", "f", "query", "search");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            Console.WriteLine("开始修复……");
            HashSet<string> dirs = [];
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files)
                    continue;
                foreach (XmlNode file in files.ChildNodes)
                {
                    if (file["RutaLocal"] is not XmlNode localPath)
                        continue;
                    ReadOnlySpan<char> trimmed = localPath.Value.AsSpan().Trim();
                    if (trimmed.Length != localPath.Value.Length)
                    {
                        updateNotSave = true;
                        localPath.SetValue(trimmed.ToString());
                    }
                    if (file["EstadoDescarga"] is { Value: "Completado" } state)
                    {
                        if (file["Porcentaje"]?.Value != "100"
                            || file["TamanoBytes"]?.Value != file["BytesDescargados"]?.Value)
                        {
                            updateNotSave = true;
                            state.SetValue("EnCola");
                        }
                        continue;
                    }
                    if (dirs.Add(localPath.Value) && !Directory.Exists(localPath.Value))
                    {
                        WriteLine(Console.Out,
                            $"创建文件夹 {DarkYellowFG}\"{YellowFG}{localPath.Value}{DarkYellowFG}\"{Reset}");
                        Directory.CreateDirectory(localPath.Value);
                    }
                    try
                    {
                        FileInfo fi = new(Path.Combine(localPath.Value, file["NombreFichero"]?.Value ?? ""));
                        if (fi.Exists)
                        {
                            if (long.TryParse(file["TamanoBytes"]?.Value, out long length) && length != fi.Length)
                                continue;
                            string lengthStr = length.ToString();
                            file.GetChildOrAddNew("TamanoBytes").SetValue(lengthStr);
                            file.GetChildOrAddNew("BytesDescargados").SetValue(lengthStr);
                            file.GetChildOrAddNew("NumeroConexionesAbiertas").SetValue("0");
                            file.GetChildOrAddNew("NumeroChunksAsignados").SetValue("0");
                            file.GetChildOrAddNew("Porcentaje").SetValue("100");
                            file.GetChildOrAddNew("DescargaComenzada").SetValue("True");
                            file.GetChildOrAddNew("DescargaProcesada").SetValue("True");
                            file.GetChildOrAddNew("ExtraccionFicheroAutomatica").SetValue("False");
                            file.GetChildOrAddNew("VelocidadKBs").SetValue("0");
                            file.GetChildOrAddNew("EstadoDescarga").SetValue("Completado");
                            file.GetChildOrAddNew("MarcadoParaBorrarFicheroLocal").SetValue("False");
                            file.GetChildOrAddNew("TiempoEstimadoDescarga").SetValue("");
                            file.GetChildOrAddNew("PausaIndividual").SetValue("False");
                            file.GetChildOrAddNew("DescargaIndividual").SetValue("False");
                            file.GetChildOrAddNew("LimiteVelocidad").SetValue("0");
                            file.GetChildOrAddNew("DatosPartes").SetValue("");
                            WriteLine(Console.Out,
                                $"完成文件 {DarkYellowFG}\"{YellowFG}{fi.FullName}{DarkYellowFG}\"{Reset}");
                        }
                    }
                    catch { }
                }
            }
            Console.WriteLine("修复完成！");
            return false;
        }, "fix", "fx");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            Console.WriteLine("基本信息：");
            WriteLine(Console.Out,
                $"{CyanFG}{xml.ChildCount}{Reset}个包");
            foreach (XmlNode package in xml.ChildNodes)
                if (package["ListaFicheros"] is XmlNode files && package["Nombre"] is XmlNode packageName)
                    WriteLine(Console.Out,
                        $"+ {WhiteFG}包 {DarkYellowFG}\"{YellowFG}{packageName.Value}{DarkYellowFG}\"{DarkGrayFG}：{CyanFG}{files.ChildCount}{Reset}个文件");
            return false;
        }, "info", "i", "information");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            Console.WriteLine("文件列表：");
            WriteLine(Console.Out,
                $"{CyanFG}{xml.ChildCount}{Reset}个包");
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files || package["Nombre"] is not XmlNode packageName)
                    continue;
                WriteLine(Console.Out,
                    $"- {WhiteFG}包 {DarkYellowFG}\"{YellowFG}{packageName.Value}{DarkYellowFG}\"{DarkGrayFG}：{CyanFG}{files.ChildCount}{Reset}个文件");
                foreach (XmlNode file in files.ChildNodes)
                {
                    if (file["URL"] is not XmlNode urlNode
                        || file["NombreFichero"] is not XmlNode fileName)
                        continue;
                    string fileNameValue = Path.Combine(file["RutaRelativa"]?.Value ?? "", fileName.Value);
                    WriteLine(Console.Out,
                        $"  - {WhiteFG}文件 {DarkYellowFG}\"{YellowFG}{fileNameValue}{DarkYellowFG}\"{DarkGrayFG}：{Reset}");
                    if (file["URL"] is XmlNode url)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}URL{DarkGrayFG}: {GreenFG}{DecryptString(url.Value)}{Reset}");
                    if (file["FileID"] is XmlNode id)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}FileID{DarkGrayFG}: {BlueFG}{DecryptString(id.Value)}{Reset}");
                    if (file["FileKey"] is XmlNode key)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}FileKey{DarkGrayFG}: {BlueFG}{DecryptString(key.Value)}{Reset}");
                    if (file["Porcentaje"] is XmlNode percent)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}Porcentaje{DarkGrayFG}: {CyanFG}{percent.Value}{Reset}");
                    if (file["EstadoDescarga"] is XmlNode est)
                        WriteLine(Console.Out,
                            $"    > {WhiteFG}EstadoDescarga{DarkGrayFG}: {CyanFG}{est.Value}{Reset}");
                }
            }
            return false;
        }, "list", "l");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            if (updateNotSave)
            {
                Console.WriteLine("有未保存的修改！是否保存？");
                if (ReadBoolean())
                    WriteXML(xmlPath, xml);
            }
            updateNotSave = false;
            xml = ReadXML(xmlPath);
            Console.WriteLine("重新加载成功！");
            return false;
        }, "reload", "r");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            int counter = 0;
            foreach (XmlNode package in xml.ChildNodes)
            {
                string p = (++counter).ToString();
                if (package["Prioridad"] is not XmlNode priority)
                {
                    priority = new XmlNode("Prioridad");
                    package.AppendChild(priority);
                }
                priority.SetValue(p);

                if (package["ListaFicheros"] is not XmlNode files)
                    continue;
                foreach (XmlNode file in files.ChildNodes)
                {
                    p = (++counter).ToString();
                    if (file["Prioridad"] is not XmlNode childPriority)
                    {
                        childPriority = new XmlNode("Prioridad");
                        file.AppendChild(childPriority);
                    }
                    childPriority.SetValue(p);
                }
            }
            updateNotSave = true;
            Console.WriteLine("设置优先级完成！");
            return false;
        }, "priority", "p");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            WriteXML(xmlPath, xml);
            updateNotSave = false;
            Console.WriteLine("保存成功！");
            return false;
        }, "save", "s");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            ReadOnlySpan<char> arg1;
            if (!args.IsEmpty)
                arg1 = args[0];
            else
            {
                Console.WriteLine("输入路径：");
                arg1 = Console.ReadLine().AsSpan().Trim().Trim('"');
            }
            WriteXML(arg1.ToString(), xml);
            Console.WriteLine("保存成功！");
            return false;
        }, "save-as", "sa", "saveas");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            if (args.IsEmpty)
            {
                WriteLine(Console.Out,
                    $"""
                    选择排序模式：
                    arg1{DarkGrayFG}:{Reset}[{WhiteFG}priority{DarkGrayFG}/{WhiteFG}size{DarkGrayFG}/{WhiteFG}percent{DarkGrayFG}/{WhiteFG}name{DarkGrayFG}/{WhiteFG}url{DarkGrayFG}/{WhiteFG}localpath{DarkGrayFG}/{WhiteFG}relativepath{Reset}]
                    arg2{DarkGrayFG}:{Reset}[{WhiteFG}asc{DarkGrayFG}/{WhiteFG}desc{Reset}]
                    """);
                args = ParseCommand(Console.ReadLine());
            }
            args.First2(out ReadOnlySpan<char> arg1, out ReadOnlySpan<char> arg2);
            if (arg2.IsEmpty)
            {
                Console.WriteLine("请指定升序还是降序！");
                return false;
            }
            bool? asc = arg2 switch
            {
                "asc" => true,
                "desc" => false,
                _ => null,
            };
            if (asc is null)
            {
                Console.WriteLine("参数无效");
                return false;
            }
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files)
                    continue;
                Span<XmlNode> nodeSpan = CollectionsMarshal.AsSpan(files.ChildNodes);
                using IMemoryOwner<XmlNode> tmpMemory = MemoryPool<XmlNode>.Shared.Rent(nodeSpan.Length);
                switch (arg1)
                {
                    case "priority" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PriorityAsc);
                        break;
                    case "priority":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PriorityDesc);
                        break;
                    case "size" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.SizeAsc);
                        break;
                    case "size":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.SizeDesc);
                        break;
                    case "percent" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PercentAsc);
                        break;
                    case "percent":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PercentDesc);
                        break;
                    case "name" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileNameAsc);
                        break;
                    case "name":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileNameDesc);
                        break;
                    case "url" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileUrlAsc);
                        break;
                    case "url":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileUrlDesc);
                        break;
                    case "localpath" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileLocalPathAsc);
                        break;
                    case "localpath":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileLocalPathDesc);
                        break;
                    case "relativepath" when asc is true:
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileRelativePathAsc);
                        break;
                    case "relativepath":
                        nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.FileRelativePathDesc);
                        break;
                    default:
                        Console.WriteLine("排序模式无效");
                        return false;
                }
            }
            updateNotSave = true;
            Console.WriteLine("排序完成！");
            return false;
        }, "sort-file", "sf");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            if (args.IsEmpty)
            {
                WriteLine(Console.Out,
                    $"""
                    选择排序模式：
                    arg1{DarkGrayFG}:{Reset}[{WhiteFG}priority{DarkGrayFG}/{WhiteFG}size{DarkGrayFG}/{WhiteFG}percent{DarkGrayFG}/{WhiteFG}name{DarkGrayFG}/{WhiteFG}localpath{Reset}]
                    arg2{DarkGrayFG}:{Reset}[{WhiteFG}asc{DarkGrayFG}/{WhiteFG}desc{Reset}]
                    """);
                args = ParseCommand(Console.ReadLine());
            }
            args.First2(out ReadOnlySpan<char> arg1, out ReadOnlySpan<char> arg2);
            bool? asc = arg2 switch
            {
                "asc" => true,
                "desc" => false,
                _ => null,
            };
            if (asc is null)
            {
                Console.WriteLine("参数无效");
                return false;
            }
            Span<XmlNode> nodeSpan = CollectionsMarshal.AsSpan(xml.ChildNodes);
            using IMemoryOwner<XmlNode> tmpMemory = MemoryPool<XmlNode>.Shared.Rent(nodeSpan.Length);
            switch (arg1)
            {
                case "priority" when asc is true:
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PriorityAsc);
                    break;
                case "priority":
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PriorityDesc);
                    break;
                case "size" when asc is true:
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.SizeAsc);
                    break;
                case "size":
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.SizeDesc);
                    break;
                case "percent" when asc is true:
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PercentAsc);
                    break;
                case "percent":
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PercentDesc);
                    break;
                case "name" when asc is true:
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PackageNameAsc);
                    break;
                case "name":
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PackageNameDesc);
                    break;
                case "localpath" when asc is true:
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PackageLocalPathAsc);
                    break;
                case "localpath":
                    nodeSpan.MergeSort(tmpMemory.Memory.Span, FieldComparers.PackageLocalPathDesc);
                    break;
                default:
                    Console.WriteLine("排序模式无效");
                    return false;
            }
            updateNotSave = true;
            Console.WriteLine("排序完成！");
            return false;
        }, "sort-pkg", "sp");
        RegisterCommand(static (ref updateNotSave, ref xml, args) =>
        {
            Console.WriteLine("请输入要重置项目的URL，以空行结束：");
            HashSet<string> urls = [];
            string? line;
            while (!string.IsNullOrEmpty(line = Console.ReadLine()))
                urls.Add(line.Trim());
            WriteLine(Console.Out,
                $"""
                            {Reset}请输入重置级别：
                            {Reset}[{CyanFG}0{DarkGrayFG}：{WhiteFG}仅重置状态{Reset}]
                            {Reset}[{CyanFG}1{DarkGrayFG}：{WhiteFG}重置状态和进度{Reset}]
                            {Reset}[{CyanFG}2{DarkGrayFG}：{WhiteFG}重置状态和进度，并移动文件到回收站{Reset}]
                            {Reset}[{CyanFG}3{DarkGrayFG}：{WhiteFG}重置状态和进度，并删除文件{Reset}]
                            """);
            long mode = ReadNumber(0, 3);
            bool changed = false;
            foreach (XmlNode package in xml.ChildNodes)
            {
                if (package["ListaFicheros"] is not XmlNode files)
                    continue;
                foreach (XmlNode file in files.ChildNodes)
                {
                    if (!file.HasAttribute(v2))
                    {
                        Console.WriteLine("仅支持v2文件信息");
                        continue;
                    }
                    if (file["URL"] is not XmlNode urlNode)
                        continue;
                    string url = DecryptString(urlNode.Value);
                    if (urls.Contains(url))
                    {
                        Console.WriteLine($"URL {GreenFG}{url}{Reset}");
                        if (file["RutaRelativa"] is not XmlNode relativePath || file["NombreFichero"] is not XmlNode fileName)
                            continue;
                        Console.WriteLine($"文件 {DarkYellowFG}\"{YellowFG}{Path.Combine(relativePath.Value, fileName.Value)}{DarkYellowFG}\"{Reset}");
                        changed = true;
                        switch (mode)
                        {
                            case 3 when file["RutaLocal"] is XmlNode localPath:
                                string filePath1 = Path.Combine(localPath.Value, fileName.Value);
                                WriteLine(Console.Out,
                                    $"  * 删除{DarkYellowFG}\"{YellowFG}{filePath1}{DarkYellowFG}\"{Reset}");
                                if (File.Exists(filePath1))
                                    File.Delete(filePath1);
                                goto case 1;
                            case 2 when file["RutaLocal"] is XmlNode localPath:
                                string filePath2 = Path.Combine(localPath.Value, fileName.Value);
                                WriteLine(Console.Out,
                                    $"  * 将{DarkYellowFG}\"{YellowFG}{filePath2}{DarkYellowFG}\"{Reset}移动到回收站");
                                if (File.Exists(filePath2))
                                    FileSystem.DeleteFile(filePath2, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                goto case 1;
                            case 3:
                            case 2:
                            case 1:
                                if (file["BytesDescargados"] is XmlNode bytesDownloaded)
                                {
                                    WriteLine(Console.Out,
                                        $"  * BytesDescargados{DarkGrayFG}: {CyanFG}{bytesDownloaded.Value}{Reset} -> {CyanFG}0{Reset}");
                                    bytesDownloaded.SetValue("0");
                                }
                                if (file["Porcentaje"] is XmlNode percent)
                                {
                                    WriteLine(Console.Out,
                                        $"  * Porcentaje{DarkGrayFG}: {CyanFG}{percent.Value}{Reset} -> {CyanFG}0{Reset}");
                                    percent.SetValue("0");
                                }
                                if (file["DatosPartes"] is XmlNode dataParts)
                                {
                                    if (dataParts["AllFinished"] is XmlNode allFinished)
                                    {
                                        WriteLine(Console.Out,
                                            $"  * AllFinished{DarkGrayFG}: {BlueFG}{allFinished.Value}{Reset} -> {BlueFG}False{Reset}");
                                        allFinished.SetValue("False");
                                    }
                                    if (dataParts["ChunkList"] is XmlNode chunkList)
                                    {
                                        foreach (XmlNode chunk in chunkList.ChildNodes)
                                        {
                                            chunk["Index"]?.SetValue("0");
                                            chunk["Available"]?.SetValue("True");
                                        }
                                        WriteLine(Console.Out,
                                            $"  * 重置{CyanFG}{chunkList.ChildCount}{Reset}个区块");
                                    }
                                }
                                goto case 0;
                            case 0:
                                if (file["EstadoDescarga"] is XmlNode downloadStatus)
                                {
                                    WriteLine(Console.Out,
                                        $"  * EstadoDescarga{DarkGrayFG}: {BlueFG}{downloadStatus.Value}{Reset} -> {BlueFG}False{Reset}");
                                    downloadStatus.SetValue("EnCola");
                                }
                                break;
                        }
                    }
                }
            }
            if (changed)
            {
                updateNotSave = true;
                Console.WriteLine("XML已修改，记得保存！");
            }
            return false;
        }, "reset", "rst");
    }
    static void RegisterCommand(CommandHandler command, params ReadOnlySpan<string> names)
    {
        foreach (string name in names)
            if (!commandRegistry.TryAdd(name, command))
                throw new ArgumentException($"name \"{name}\" already used!");
    }
    static void Main()
    {
        Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;
        XmlNode xml = ReadXML(xmlPath);
        bool updateNotSave = false;
        while (true)
        {
            Console.WriteLine("请输入操作：");
            string[] commandInput = ParseCommand(Console.ReadLine());
            if (commandInput.Length < 1
                || !commandRegistry.TryGetValue(commandInput[0], out CommandHandler? command))
                continue;
            try
            {
                if (command(ref updateNotSave, ref xml, commandInput.AsSpan(1)))
                    break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
    static bool ReadBoolean()
    {
        WriteLine(Console.Out,
            $"[{GreenFG}Y{DarkGrayFG}/{RedFG}N{Reset}] >>> ");
        ReadOnlySpan<char> input = Console.ReadLine().AsSpan().Trim();
        return "true".StartsWith(input, StringComparison.OrdinalIgnoreCase)
            || "yes".StartsWith(input, StringComparison.OrdinalIgnoreCase);
    }
    static long ReadNumber(long min, long max)
    {
        string prompt = $"[{min}~{max}] >>> ";
        Console.Write(prompt);
        ReadOnlySpan<char> input = Console.ReadLine().AsSpan().Trim();
        long num;
        while (!long.TryParse(input, out num) || num < min || num > max)
        {
            Console.WriteLine("请重试");
            Console.Write(prompt);
            input = Console.ReadLine().AsSpan().Trim();
        }
        return num;
    }
    static XmlNode ReadXML(string path)
    {
        XmlReaderSettings settings = new()
        {
            IgnoreWhitespace = true
        };
        using XmlReader reader = XmlReader.Create(path, settings);
        return XmlNode.ReadFrom(reader, out _) ?? throw new InvalidDataException();
    }
    static void WriteXML(string path, XmlNode node)
    {
        XmlWriterSettings settings = new()
        {
            OmitXmlDeclaration = true,
            Indent = true
        };
        using XmlWriter writer = XmlWriter.Create(path, settings);
        node.WriteTo(writer);
    }
    static string[] ParseCommand(ReadOnlySpan<char> command)
    {
        List<string> args = [];
        command = command.Trim();
        bool q = false;
        int from = 0, to = 0;
        for (; to < command.Length; to++)
        {
            char c = command[to];
            switch (c)
            {
                case ' ' when !q:
                    if (from != to)
                        args.Add(new(command[from..to]));
                    from = to + 1;
                    break;
                case '"':
                    if (q)
                    {
                        args.Add(new(command[from..to]));
                        to++;
                        from = to;
                    }
                    else
                    {
                        from++;
                    }
                    q = !q;
                    break;
            }
        }
        if (from < command.Length && to <= command.Length && from != to)
            args.Add(new(command[from..to]));
        return [.. args];
    }
    public static string DecryptString(string data)
    {
        byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(data), DataProtectionScope.CurrentUser, Entropy);
        return Encoding.Unicode.GetString(bytes);
    }
    static void MergeSort<T>(this Span<T> arr, Span<T> tmp, IComparer<T> comparer)
    {
        if (arr.Length <= 2)
        {
            if (arr.Length == 2)
            {
                ref T v0 = ref arr[0];
                ref T v1 = ref arr[1];
                if (comparer.Compare(v0, v1) > 0)
                    (v0, v1) = (v1, v0);
            }
            return;
        }
        int mid = ((arr.Length - 1) >> 1) + 1;
        int i = 0;
        int j = mid;
        int k = 0;
        MergeSort(arr[..mid], tmp, comparer);
        MergeSort(arr[mid..], tmp, comparer);
        while (i < mid && j < arr.Length)
            if (comparer.Compare(arr[i], arr[j]) <= 0)
                tmp[k++] = arr[i++];
            else
                tmp[k++] = arr[j++];
        if (i < mid)
            arr[i..mid].CopyTo(arr[k..]);
        tmp[..k].CopyTo(arr);
    }
    static void AddItemOrInitList<TKey, TAlternate, TItem>(
        this Dictionary<TKey, List<TItem>>.AlternateLookup<TAlternate> lookup,
        TAlternate key,
        TItem value)
        where TKey : notnull
        where TAlternate : notnull, allows ref struct
    {
        ref List<TItem>? list = ref CollectionsMarshal.GetValueRefOrAddDefault(lookup, key, out _);
        if (list is not null)
            list.Add(value);
        else
            list = [value];
    }
    static void AddItemOrInitList<TKey, TItem>(
        this Dictionary<TKey, List<TItem>> dict,
        TKey key,
        TItem value)
        where TKey : notnull
    {
        ref List<TItem>? list = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out _);
        if (list is not null)
            list.Add(value);
        else
            list = [value];
    }
    static void First2(this ReadOnlySpan<string> args, out ReadOnlySpan<char> arg1, out ReadOnlySpan<char> arg2)
    {
        arg1 = args.Length > 0 ? args[0] : [];
        arg2 = args.Length > 1 ? args[1] : [];
    }
    static ReadOnlySpan<byte> Entropy => [
//      G         *         S         N         A         f         h         H
        0x47,0x00,0x2A,0x00,0x53,0x00,0x4E,0x00,0x41,0x00,0x66,0x00,0x68,0x00,0x48,0x00,
//      W         5         A         ¿         A         m         c         k
        0x57,0x00,0x35,0x00,0x41,0x00,0xBF,0x00,0x41,0x00,0x6D,0x00,0x63,0x00,0x6B,0x00,
//      +         X         M         L         C         M         6         M
        0x2B,0x00,0x58,0x00,0x4D,0x00,0x4C,0x00,0x43,0x00,0x4D,0x00,0x36,0x00,0x4D,0x00,
//      #         $         x         E         K         ;         9         q
        0x23,0x00,0x24,0x00,0x78,0x00,0x45,0x00,0x4B,0x00,0x3B,0x00,0x39,0x00,0x71,0x00];

    sealed class DeDupComparer : IComparer<(XmlNode, XmlNode)>
    {
        public static readonly DeDupComparer Instance = new();
        public int Compare((XmlNode, XmlNode) x, (XmlNode, XmlNode) y)
        {
            bool xb = IsCompleted(x.Item1);
            bool yb = IsCompleted(y.Item1);
            return yb.CompareTo(xb);
        }
        public static bool IsCompleted(XmlNode x)
        {
            return x?["RutaLocal"] is not null && "Completado".Equals(x["EstadoDescarga"]?.Value, StringComparison.OrdinalIgnoreCase);
        }
    }
}