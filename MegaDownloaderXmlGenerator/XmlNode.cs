using System.Xml;

namespace MegaDownloaderXmlGenerator;

/// <summary>
/// Warning: Only a subset of XML is supported.
/// <list type="bullet">
/// <item>Mixed text and elements as child nodes are not allowed</item>
/// <item>Other node types (such as CDATA and Entity) are not allowed</item>
/// </list>
/// </summary>
public sealed class XmlNode(string name)
{
    // not recommend to change name
    public string Name { get; set; } = name;
    public string Value { get; private set; } = "";
    public List<KeyValuePair<string, string>> Attributes { get; } = [];
    public List<XmlNode> ChildNodes { get; } = [];
    public XmlNode AppendChild(XmlNode node)
    {
        Value = "";
        ChildNodes.Add(node);
        return this;
    }
    public XmlNode AppendAttribute(string key, string value)
    {
        return AppendAttribute(new(key, value));
    }
    public XmlNode AppendAttribute(KeyValuePair<string, string> attribute)
    {
        Attributes.Add(attribute);
        return this;
    }
    public XmlNode SetValue(string text)
    {
        Value = text;
        ChildNodes.Clear();
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
}