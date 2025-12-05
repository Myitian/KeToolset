using Microsoft.VisualBasic.FileIO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace MegaDownloaderTaskReset;

class Program
{
    static readonly KeyValuePair<string, string> v2 = new("v", "2");

    static void Main()
    {
        string xmlPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"MegaDownloader\Config\DownloadList.xml");
        XmlNode xml = ReadXML(xmlPath);
        bool updateNotSave = false;
        while (true)
        {
            Console.WriteLine("请输入操作：");
            string[] command = ParseCommand(Console.ReadLine());
            if (command.Length < 1)
                continue;
            ReadOnlySpan<char> arg = command.Length > 1 ? command[1] : [];
            try
            {
                switch (command[0].ToLower())
                {
                    case "q":
                    case "quit":
                    case "exit":
                        if (updateNotSave)
                        {
                            Console.WriteLine("有未保存的修改！是否保存？");
                            if (ReadBoolean())
                                WriteXML(xmlPath, xml);
                        }
                        Console.WriteLine("是否确认退出？");
                        if (ReadBoolean())
                            return;
                        else
                            break;
                    case "?":
                    case "h":
                    case "help":
                        Console.WriteLine("帮助：");
                        Console.WriteLine(" 命令    | 参数     | 别名             | 说明");
                        Console.WriteLine(" quit      无         q, exit            退出");
                        Console.WriteLine(" help      无         ?, h               帮助");
                        Console.WriteLine(" version   无         v, ver             版本");
                        Console.WriteLine(" info      无         i, information     基本信息");
                        Console.WriteLine(" list      无         l                  文件列表");
                        Console.WriteLine(" reload    无         r                  重新加载");
                        Console.WriteLine(" fix       无         fx                 修复不完整的文件夹结构");
                        Console.WriteLine(" save      无         s                  保存");
                        Console.WriteLine(" save-as   文件路径   sa, saveas         另存为");
                        Console.WriteLine(" find      查询内容   f, query, search   查询");
                        Console.WriteLine(" reset     无         rst                重置下载状态");
                        break;
                    case "v":
                    case "ver":
                    case "version":
                        AssemblyName name = Assembly.GetExecutingAssembly().GetName();
                        Console.WriteLine($"{name.Name} v{name.Version}");
                        break;
                    case "i":
                    case "info":
                    case "information":
                        Console.WriteLine("基本信息：");
                        Console.WriteLine($"{xml.ChildNodes.Count}个包");
                        foreach (XmlNode package in xml.ChildNodes)
                            if (package["ListaFicheros"] is XmlNode files && package["Nombre"] is XmlNode packageName)
                                Console.WriteLine($"+ 包 \"{packageName.Value}\"：{files.ChildNodes.Count}个文件");
                        break;
                    case "l":
                    case "list":
                        Console.WriteLine("文件列表：");
                        Console.WriteLine($"{xml.ChildNodes.Count}个包");
                        foreach (XmlNode package in xml.ChildNodes)
                        {
                            if (package["ListaFicheros"] is not XmlNode files || package["Nombre"] is not XmlNode packageName)
                                continue;
                            Console.WriteLine($"- 包 \"{packageName.Value}\"：{files.ChildNodes.Count}个文件");
                            foreach (XmlNode file in files.ChildNodes)
                            {
                                if (file["URL"] is not XmlNode urlNode
                                    || file["NombreFichero"] is not XmlNode fileName)
                                    continue;
                                Console.WriteLine($"  - 文件 \"{Path.Combine(file["RutaRelativa"]?.Value ?? "", fileName.Value)}\"");
                                string url = DecryptString(urlNode.Value);
                                Console.WriteLine($"    - URL: {url}");
                                if (file["FileID"] is XmlNode id)
                                    Console.WriteLine($"    - FileID: {DecryptString(id.Value)}");
                                if (file["FileKey"] is XmlNode key)
                                    Console.WriteLine($"    - FileKey: {DecryptString(key.Value)}");
                                if (file["Porcentaje"] is XmlNode percent)
                                    Console.WriteLine($"    - Porcentaje: {percent.Value}");
                                if (file["EstadoDescarga"] is XmlNode est)
                                    Console.WriteLine($"    - EstadoDescarga: {est.Value}");
                            }
                        }
                        break;
                    case "r":
                    case "reload":
                        if (updateNotSave)
                        {
                            Console.WriteLine("有未保存的修改！是否保存？");
                            if (ReadBoolean())
                                WriteXML(xmlPath, xml);
                        }
                        updateNotSave = false;
                        xml = ReadXML(xmlPath);
                        Console.WriteLine("重新加载成功！");
                        break;
                    case "fx":
                    case "fix":
                        Console.WriteLine("开始修复目录结构……");
                        HashSet<string> dirs = [];
                        foreach (XmlNode package in xml.ChildNodes)
                        {
                            if (package["ListaFicheros"] is not XmlNode files)
                                continue;
                            foreach (XmlNode file in files.ChildNodes)
                            {
                                if (file["RutaLocal"] is not XmlNode localPath
                                    || file["EstadoDescarga"] is { Value: "Completado" })
                                    continue;
                                string path = Path.Combine(localPath.Value, file["RutaRelativa"]?.Value ?? "");
                                if (Directory.Exists(path))
                                    dirs.Add(path);
                                else if (dirs.Add(path))
                                {
                                    Console.WriteLine($"创建文件夹 \"{path}\"");
                                    Directory.CreateDirectory(path);
                                }
                            }
                        }
                        Console.WriteLine("修复目录结构完成！");
                        break;
                    case "s":
                    case "save":
                        WriteXML(xmlPath, xml);
                        updateNotSave = false;
                        Console.WriteLine("保存成功！");
                        break;
                    case "sa":
                    case "saveas":
                    case "save-as":
                        if (arg.IsEmpty)
                        {
                            Console.WriteLine("输入路径：");
                            arg = Console.ReadLine().AsSpan().Trim().Trim('"');
                        }
                        WriteXML(arg.ToString(), xml);
                        Console.WriteLine("保存成功！");
                        break;
                    case "f":
                    case "find":
                    case "query":
                    case "search":
                        if (arg.IsEmpty)
                        {
                            Console.WriteLine("输入搜索内容：");
                            arg = Console.ReadLine().AsSpan().Trim().Trim('"');
                        }
                        foreach (XmlNode package in xml.ChildNodes)
                        {
                            if (package["ListaFicheros"] is not XmlNode files || package["Nombre"] is not XmlNode packageName)
                                continue;
                            bool first = true;
                            foreach (XmlNode file in files.ChildNodes)
                            {
                                if (file["URL"] is not XmlNode urlNode
                                    || file["RutaRelativa"] is not XmlNode relativePath
                                    || file["NombreFichero"] is not XmlNode fileName)
                                    continue;
                                string relativePathValue = relativePath.Value;
                                string fileNameValue = fileName.Value;
                                if (!relativePathValue.AsSpan().Contains(arg, StringComparison.CurrentCultureIgnoreCase)
                                    && !fileNameValue.AsSpan().Contains(arg, StringComparison.CurrentCultureIgnoreCase))
                                    continue;
                                if (first)
                                {
                                    Console.WriteLine($"- 包 \"{packageName.Value}\"：");
                                    first = false;
                                }
                                Console.WriteLine($"  - 文件 \"{Path.Combine(relativePathValue, fileNameValue)}\"");
                                string url = DecryptString(urlNode.Value);
                                Console.WriteLine($"    - URL: {url}");
                                if (file["Porcentaje"] is XmlNode percent)
                                    Console.WriteLine($"    - Porcentaje: {percent.Value}");
                                if (file["EstadoDescarga"] is XmlNode est)
                                    Console.WriteLine($"    - EstadoDescarga: {est.Value}");
                            }
                        }
                        break;
                    case "rst":
                    case "reset":
                        Console.WriteLine("请输入要重置项目的URL，以空行结束：");
                        HashSet<string> urls = [];
                        string? line;
                        while (!string.IsNullOrEmpty(line = Console.ReadLine()))
                            urls.Add(line.Trim());
                        Console.WriteLine("请输入重置级别：");
                        Console.WriteLine("[0：仅重置状态]");
                        Console.WriteLine("[1：重置状态和进度]");
                        Console.WriteLine("[2：重置状态和进度，并移动文件到回收站]");
                        Console.WriteLine("[3：重置状态和进度，并删除文件]");
                        long mode = ReadNumber(0, 3);
                        bool changed = false;
                        foreach (XmlNode package in xml.ChildNodes)
                        {
                            if (package["ListaFicheros"] is not XmlNode files)
                                continue;
                            foreach (XmlNode file in files.ChildNodes)
                            {
                                if (!file.Attributes.Contains(v2))
                                {
                                    Console.WriteLine("仅支持v2文件信息");
                                    continue;
                                }
                                if (file["URL"] is not XmlNode urlNode)
                                    continue;
                                string url = DecryptString(urlNode.Value);
                                if (urls.Contains(url))
                                {
                                    updateNotSave = true;
                                    Console.WriteLine($"URL {url}");
                                    if (file["RutaRelativa"] is not XmlNode relativePath || file["NombreFichero"] is not XmlNode fileName)
                                        continue;
                                    Console.WriteLine($"文件 \"{Path.Combine(relativePath.Value, fileName.Value)}\"");
                                    changed = true;
                                    switch (mode)
                                    {
                                        case 3 when file["RutaLocal"] is XmlNode localPath:
                                            string filePath1 = Path.Combine(localPath.Value, relativePath.Value, fileName.Value);
                                            Console.WriteLine($"  * 删除\"{filePath1}\"");
                                            if (File.Exists(filePath1))
                                                File.Delete(filePath1);
                                            goto case 1;
                                        case 2 when file["RutaLocal"] is XmlNode localPath:
                                            string filePath2 = Path.Combine(localPath.Value, relativePath.Value, fileName.Value);
                                            Console.WriteLine($"  * 将\"{filePath2}\"移动到回收站");
                                            if (File.Exists(filePath2))
                                                FileSystem.DeleteFile(filePath2, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                            goto case 1;
                                        case 3:
                                        case 2:
                                        case 1:
                                            if (file["BytesDescargados"] is XmlNode bytesDownloaded)
                                            {
                                                Console.WriteLine($"  * BytesDescargados: {bytesDownloaded.Value} -> 0");
                                                bytesDownloaded.SetValue("0");
                                            }
                                            if (file["Porcentaje"] is XmlNode percent)
                                            {
                                                Console.WriteLine($"  * Porcentaje: {percent.Value} -> 0");
                                                percent.SetValue("0");
                                            }
                                            if (file["DatosPartes"] is XmlNode dataParts)
                                            {
                                                if (dataParts["AllFinished"] is XmlNode allFinished)
                                                {
                                                    Console.WriteLine($"  * AllFinished: {allFinished.Value} -> False");
                                                    allFinished.SetValue("False");
                                                }
                                                if (dataParts["ChunkList"] is XmlNode chunkList)
                                                {
                                                    foreach (XmlNode chunk in chunkList.ChildNodes)
                                                    {
                                                        chunk["Index"]?.SetValue("0");
                                                        chunk["Available"]?.SetValue("True");
                                                    }
                                                    Console.WriteLine($"  * 重置{chunkList.ChildNodes.Count}个区块");
                                                }
                                            }
                                            goto case 0;
                                        case 0:
                                            if (file["EstadoDescarga"] is XmlNode downloadStatus)
                                            {
                                                Console.WriteLine($"  * EstadoDescarga: {downloadStatus.Value} -> EnCola");
                                                downloadStatus.SetValue("EnCola");
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                        if (changed)
                        {
                            Console.WriteLine("XML已修改，记得保存！");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
    static bool ReadBoolean()
    {
        Console.Write("[Y/N] >>> ");
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
    static ReadOnlySpan<byte> Entropy => [
//      G         *         S         N         A         f         h         H
        0x47,0x00,0x2A,0x00,0x53,0x00,0x4E,0x00,0x41,0x00,0x66,0x00,0x68,0x00,0x48,0x00,
//      W         5         A         ¿         A         m         c         k
        0x57,0x00,0x35,0x00,0x41,0x00,0xBF,0x00,0x41,0x00,0x6D,0x00,0x63,0x00,0x6B,0x00,
//      +         X         M         L         C         M         6         M
        0x2B,0x00,0x58,0x00,0x4D,0x00,0x4C,0x00,0x43,0x00,0x4D,0x00,0x36,0x00,0x4D,0x00,
//      #         $         x         E         K         ;         9         q
        0x23,0x00,0x24,0x00,0x78,0x00,0x45,0x00,0x4B,0x00,0x3B,0x00,0x39,0x00,0x71,0x00];
}
