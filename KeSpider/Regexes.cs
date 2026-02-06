using System.Text.RegularExpressions;

namespace KeSpider;

static partial class Regexes
{
    [GeneratedRegex(@"(?:https?://)?(?<domain>[^/]+)/(?:api/v\d/)?(?<service>[^/]+)/(?:user/)?(?<user>[^/\?#]+)")]
    internal static partial Regex RegMainPage();

    [GeneratedRegex(@"(?:https?://)?(?<domain>[^/]+)/(?:api/v\d/)?(?<service>[^/]+)/(?:user/)?(?<user>[^/]+)/(?:post/)?(?<id>[^/\?#]+)")]
    internal static partial Regex RegPostPage();

    [GeneratedRegex(@"(?<url>https?:[/\\]{2}(?:[^\x00-\x1f \x7f""<>\^`\{\|\}\.\\/\?#]+\.)+[^\x00-\x1f \x7f""<>\^`\{\|\}\.\\/\?#]+(?:[/\\\?#][^\x00-\x1f \x7f""<>\^`\{\|\}]*)*)")]
    internal static partial Regex RegUrl();

    [GeneratedRegex(@"(?<server>(?:https://[^/]+)?)(?<path>/(?:[0-9a-fA-F]{2}/){2}(?<name>[0-9a-fA-F]+\.[0-9A-Za-z]+))")]
    internal static partial Regex RegInlineFile();

    [GeneratedRegex(@"(?<url>https?://mega(?:\.co)?\.nz/[^""'<>\s]+)(?:<[^\>]+>)?(?<ext>#[a-zA-Z0-9\-_]+)")]
    internal static partial Regex RegMega();

    [GeneratedRegex(@"(?<url>https?://pan\.baidu\.com/s/[^<>""?]+)(?:[\S\s]*?(?:提取码|p(?:ass)?w(?:or)?d)\s*[：:=]\s*(?<ext>[\dA-Za-z]{4}))?")]
    internal static partial Regex RegBaiduPan();

    [GeneratedRegex(@"(?<url>https?://mypikpak\.com/s/[a-zA-Z0-9\-_]+)")]
    internal static partial Regex RegPikPak();


    [GeneratedRegex(@"\.(?<num>\d+)$")]
    internal static partial Regex RegMultiPartNumberOnly();

    [GeneratedRegex(@"\.part(?<num>\d+)\.rar$")]
    internal static partial Regex RegMultiPartRar();

    [GeneratedRegex(@"\.r(?<num>\d+)$")]
    internal static partial Regex RegMultiPartRxx();

    [GeneratedRegex(@"\.z(?<num>\d+)$")]
    internal static partial Regex RegMultiPartZxx();
}
