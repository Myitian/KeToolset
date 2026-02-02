using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeCore.API;

public record struct Archive
{
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    public static async Task<Archive> Request(HttpClient client, string domain, string hash, int retry = 3)
    {
        ArgumentNullException.ThrowIfNull(client);
        Uri url = new UriBuilder()
        {
            Scheme = "https",
            Host = domain,
            Path = $"api/v1/file/{hash}"
        }.Uri;
        while (true)
        {
            Console.WriteLine($"GET {url}");
            using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).C();
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.Archive).C();
            Console.WriteLine($"HTTP STATUS CODE {resp.StatusCode}");
            if (--retry == 0)
                return new();
            await Task.Delay(1000).C();
        }
    }
}