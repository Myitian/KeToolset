using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

sealed class SimpleOutlinkHandler(string name, Regex pattern, string? seperator = null) : IOutlinkHandler
{
    public Regex Pattern => pattern;
    public ValueTask ProcessMatches(
        PostContext context,
        string content,
        HashSet<string> usedLinks,
        params IEnumerable<Match> matches)
    {
        foreach (Match m in matches)
        {
            if (!m.Success)
                continue;
            string text = m.Groups["url"].Value;
            if (m.Groups.ContainsKey("ext"))
                text += seperator + m.Groups["ext"].Value;
            if (!usedLinks.Add(text))
                continue;
            PostContext.Log(IOutlinkHandler.MODE, $"Find Outlink of {name}: {text}");
            context.OutlinkCounter++;
            string fileName = Utils.ReplaceInvalidFileNameChars(text) + ".placeholder.txt";
            string path = Path.Combine(context.PageFolderPath, fileName);
            if (Program.SaveModeContent == SaveMode.Skip && File.Exists(path))
            {
                PostContext.Log(IOutlinkHandler.MODE, "Skipped");
                Utils.SetTime(path, context.Datetime, context.DatetimeEdited);
            }
            else
                Utils.SaveFile(text, fileName, context.PageFolderPath, context.Datetime, context.DatetimeEdited, Program.SaveModeOutlink);
        }
        return ValueTask.CompletedTask;
    }
}
