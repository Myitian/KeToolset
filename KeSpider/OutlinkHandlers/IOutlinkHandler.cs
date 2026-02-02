using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

interface IOutlinkHandler
{
    public const string MODE = "OUTLINK";
    public abstract Regex Pattern { get; }
    public abstract ValueTask ProcessMatches(
        PostContext context,
        string content,
        HashSet<string> usedLinks,
        params IEnumerable<Match> matches);
}
