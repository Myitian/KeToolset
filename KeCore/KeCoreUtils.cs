using KeCore.API;
using System.Runtime.CompilerServices;

namespace KeCore;

public static class KeCoreUtils
{
    internal static ConfiguredTaskAwaitable C(this Task task)
        => task.ConfigureAwait(false);
    internal static ConfiguredTaskAwaitable<TResult> C<TResult>(this Task<TResult> task)
        => task.ConfigureAwait(false);
    internal static ConfiguredCancelableAsyncEnumerable<TResult> C<TResult>(this IAsyncEnumerable<TResult> results)
        => results.ConfigureAwait(false);
    public static async Task<HashSet<PostInfo>> LoadAllPosts(
        HttpClient client,
        string domain,
        string service,
        string user,
        Predicate<PostsResult>? predicate = null)
    {
        HashSet<PostInfo> posts = [];
        int offset = 0;
        int pageSize;
        do
        {
            pageSize = 50;
            await foreach (PostsResult post in PostsResult.Request(client, domain, service, user, offset).C())
            {
                if (predicate?.Invoke(post) is false)
                    break;
                posts.Add(new(post, domain));
                offset++;
                pageSize--;
            }
        }
        while (pageSize == 0);
        return posts;
    }
}