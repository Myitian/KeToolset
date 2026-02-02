using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeCore.API;

public record struct Attachment
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("stem")]
    public string Stem { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}

public record struct Embed
{
    [JsonPropertyName("url")]
    public string? URL { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("description")]
    public object? Description { get; set; }
}

public record struct Post
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("published")]
    public DateTime Published { get; set; }

    [JsonPropertyName("edited")]
    public DateTime? Edited { get; set; }

    [JsonPropertyName("file")]
    public Attachment File { get; set; }

    [JsonPropertyName("embed")]
    public Embed Embed { get; set; }

    [JsonPropertyName("attachments")]
    public ImmutableArray<Attachment> Attachments { get; set; }
}

public class PostRoot
{
    [JsonPropertyName("post")]
    public Post Post { get; set; }

    [JsonPropertyName("attachments")]
    public ImmutableArray<Attachment> Attachments { get; set; }

    [JsonPropertyName("previews")]
    public ImmutableArray<Attachment> Previews { get; set; }

    public static async Task<(byte[], PostRoot?)> Request(HttpClient client, string domain, string service, string user, string post)
    {
        ArgumentNullException.ThrowIfNull(client);
        Uri url = new UriBuilder()
        {
            Scheme = "https",
            Host = domain,
            Path = $"/api/v1/{service}/user/{user}/post/{post}"
        }.Uri;
        while (true)
        {
            Console.WriteLine($"GET {url}");
            using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead).C();
            if (resp.IsSuccessStatusCode)
            {
                return (
                    await resp.Content.ReadAsByteArrayAsync().C(),
                    await resp.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.PostRoot).C());
            }
            Console.WriteLine($"HTTP STATUS CODE {resp.StatusCode}");
            await Task.Delay(1000).C();
        }
    }
}