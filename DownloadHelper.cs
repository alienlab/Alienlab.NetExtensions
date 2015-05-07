namespace Alienlab.NetExtensions
{
  public static class DownloadHelper
  {
    /// <summary>
    /// Download large file in several threads. It is optimized for files larger than 6MB.
    /// </summary>
    [NotNull]
    public static DownloadContext CreateContext([NotNull] DownloadOptions options)
    {
      Assert.IsNotNull(options, "options");

      return new DownloadContext(options);
    }
  }
}