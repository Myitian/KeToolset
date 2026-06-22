using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeCore.API;

public record struct Comment
{
    [JsonPropertyName("id")]
    public string ID { get; set; }
    [JsonPropertyName("content")]
    public string Content { get; set; }

    public static async Task<(byte[]?, List<Comment>?)> Request(HttpClient client, string domain, string service, string user, string post)
    {
        ArgumentNullException.ThrowIfNull(client);
        Uri url = new UriBuilder()
        {
            Scheme = "https",
            Host = domain,
            Path = $"/api/v1/{service}/user/{user}/post/{post}/comments"
        }.Uri;
        while (true)
        {
            Console.WriteLine($"GET {url}");
            using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).C();
            if (resp.IsSuccessStatusCode)
            {
                return (
                    await resp.Content.ReadAsByteArrayAsync().C(),
                    await resp.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.ListComment).C());
            }
            if (resp.StatusCode is HttpStatusCode.NotFound)
                return (null, null);
            Console.WriteLine($"HTTP STATUS CODE {resp.StatusCode}");
            await Task.Delay(1000).C();
        }
    }
}