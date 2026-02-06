using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeCore.API;

public record struct PostResult
{
    [JsonPropertyName("id")]
    public string ID { get; set; }
    [JsonPropertyName("user")]
    public string User { get; set; }
    [JsonPropertyName("service")]
    public string Service { get; set; }
    [JsonPropertyName("title")]
    public string Title { get; set; }

    public static async IAsyncEnumerable<PostResult> Request(
        HttpClient client,
        string domain,
        string service,
        string user,
        int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(client);
        Uri url = new UriBuilder()
        {
            Scheme = "https",
            Host = domain,
            Path = $"api/v1/{service}/user/{user}/posts",
            Query = $"o={offset}"
        }.Uri;
        while (true)
        {
            Console.WriteLine($"GET {url}");
            using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).C();
            if (resp.IsSuccessStatusCode)
            {
                await foreach (PostResult post in resp.Content
                    .ReadFromJsonAsAsyncEnumerable(AppJsonSerializerContext.Default.PostResult).C())
                    yield return post;
                yield break;
            }
            Console.WriteLine($"HTTP STATUS CODE {resp.StatusCode}");
            await Task.Delay(1000).C();
        }
    }
}