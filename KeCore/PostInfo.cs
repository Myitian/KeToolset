using KeCore.API;

namespace KeCore;

public readonly record struct PostInfo(string ID, string User, string Service, string Domain) : IEquatable<PostInfo>
{
    public PostInfo(PostResult post, string domain) : this(post.ID, post.User, post.Service, domain) { }
}