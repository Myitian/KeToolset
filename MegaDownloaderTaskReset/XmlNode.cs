using System.Diagnostics;
using System.Net;
using System.Xml;

namespace MegaDownloaderTaskReset;

/// <summary>
/// Warning: Only a subset of XML is supported.
/// <list type="bullet">
/// <item>no mix text and element as children</item>
/// <item>no other type of elements</item>
/// </list>
/// </summary>
[DebuggerDisplay("Name = {Name}, Value = {Value}, ChildCount = {ChildNodes.Count}")]
public sealed class XmlNode(string name)
{
    private readonly Dictionary<string, XmlNode> _map = [];
    // not recommend to change name
    public string Name { get; set; } = name;
    public string Value { get; private set; } = "";
    public List<KeyValuePair<string, string>> Attributes { get; } = [];
    public List<XmlNode> ChildNodes { get; } = [];
    public XmlNode? this[string name] => _map.TryGetValue(name, out XmlNode? node) ? node : null;
    public override string ToString()
    {
        return $"<{Name}>{WebUtility.HtmlEncode(Value)} @ {ChildNodes.Count} childs";
    }
    public XmlNode GetChildOrAddNew(string name)
    {
        if(_map.TryGetValue(name, out XmlNode? node))
            return node;
        node = new(name);
        AppendChild(node);
        return node;
    }
    public XmlNode AppendChild(XmlNode node)
    {
        Value = "";
        ChildNodes.Add(node);
        _map.TryAdd(node.Name, node);
        return this;
    }
    public XmlNode AppendAttribute(string key, string value)
    {
        Attributes.Add(new(key, value));
        return this;
    }
    public XmlNode SetValue(string text)
    {
        Value = text;
        ChildNodes.Clear();
        _map.Clear();
        return this;
    }
    public XmlNode SetValue(IEnumerable<XmlNode> nodes)
    {
        SetValue("");
        foreach (XmlNode node in nodes)
            AppendChild(node);
        return this;
    }
    public void WriteTo(XmlWriter writer)
    {
        writer.WriteStartElement(Name);
        foreach ((string key, string value) in Attributes)
            writer.WriteAttributeString(key, value);
        if (ChildNodes.Count == 0)
            writer.WriteString(Value);
        else
        {
            foreach (XmlNode child in ChildNodes)
                child.WriteTo(writer);
        }
        writer.WriteEndElement();
    }
    public static XmlNode? ReadFrom(XmlReader reader, out string? text)
    {
        text = null;
        switch (reader.MoveToContent())
        {
            case XmlNodeType.Element:
                XmlNode node = new(reader.Name);
                while (reader.MoveToNextAttribute())
                    node.Attributes.Add(new(reader.Name, reader.Value));
                bool isEmpty = reader.IsEmptyElement;
                reader.Read();
                if (!isEmpty)
                {
                    while (true)
                    {
                        XmlNode? child = ReadFrom(reader, out string? childtext);
                        if (child is not null)
                            node.AppendChild(child);
                        else if (childtext is not null)
                            node.Value = childtext;
                        else
                            break;
                    }
                }
                return node;
            case XmlNodeType.Text:
                text = reader.Value;
                goto case XmlNodeType.EndElement;
            case XmlNodeType.EndElement:
                reader.Read();
                goto default;
            default:
                return null;
        }
    }
}