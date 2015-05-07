namespace Alienlab.NetExtensions
{
  using System;
  using System.Threading;

  public sealed class DownloadOptions
  {
    public event Action<long> OnProgressChanged;

    private const long Kb = 1024;

    private const long Mb = 1024 * Kb;
    
    public Uri Uri { get; set; }
    
    public string FilePath { get; set; }
    
    public CancellationToken CancellationToken { get; set; }

    public Size NetworkReadBufferSize { get; set; } = Size.FromKilobytes(64);
    
    public string Cookies { get; set; }
    
    public IBlockOptions BlockOptions { get; set; } = new MaxBlockSize(Size.FromMegabytes(32));

    /// <summary>
    /// The maximum amount of time allowed for the file to download.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromHours(24);
  }
}