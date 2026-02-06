namespace KeSpider;

enum SaveMode
{
    /// <summary>
    /// Replace the existing file
    /// </summary>
    Replace,
    /// <summary>
    /// When the checksum verification cost is low, its effect is approximately equivalent to <see cref="Replace"/>;
    /// otherwise, it is approximately equivalent to <see cref="Skip"/>.
    /// </summary>
    ReplaceOrSkip,
    /// <summary>
    /// Skip the existing file
    /// </summary>
    Skip
}