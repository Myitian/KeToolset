using KeCore;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;
using SocketsHttpHandler handler = new()
{
    AutomaticDecompression = DecompressionMethods.All,
    AllowAutoRedirect = true,
    UseProxy = true
};
using HttpClient client = new(handler);
client.DefaultRequestHeaders.Accept.Add(new("text/css"));
Regex rMainPage = RegMainPage();
Match mm;
do
{
    Console.WriteLine("Enter Author's Page URL:");
    string url = Console.ReadLine() ?? "";
    mm = rMainPage.Match(url);
}
while (!mm.Success);
string domain = mm.Groups["domain"].Value;
string service = mm.Groups["service"].Value;
string user = mm.Groups["user"].Value;
HashSet<PostInfo> posts = await KeCoreUtils.LoadAllPosts(client, domain, service, user).ConfigureAwait(false);
foreach (PostInfo post in posts)
    Console.WriteLine(post.ID);

partial class Program
{
    [GeneratedRegex(@"https://(?<domain>[^/]+)/(?:api/v\d/)?(?<service>[^/]+)/user/(?<user>[^/\?#]+)")]
    internal static partial Regex RegMainPage();
}