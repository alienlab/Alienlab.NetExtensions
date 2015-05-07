namespace Alienlab.NetExtensions
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Threading;

  public class DownloadContext
  {
    public readonly Uri Uri;

    [NotNull]
    public readonly string FilePath;

    public readonly Size FileSize;

    public readonly long NetworkReadBufferSize;
    
    private readonly CancellationToken cancellationToken;
    
    public readonly string Cookies;

    [NotNull]
    private readonly IEnumerable<Block> Blocks;

    public long BlocksCount { get; }

    private readonly Size percentSize;

    private long totalBytesDownloaded;

    [NotNull]
    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    private int blocksCompleted;

    private readonly TimeSpan RequestTimeout;

    [NotNull]
    private readonly object syncBlock = new object();

    private bool downloadCompleted;

    public int BlocksCompleted => this.blocksCompleted;

    public event Action<Block, long> OnProgressChanged;

    public event Action<Exception> OnErrorOccurred;

    public event Action OnDownloadCompleted;

    public long TotalBytesDownloaded => this.totalBytesDownloaded;

    public long TotalPercentage { get; private set; }

    public DownloadContext([NotNull] DownloadOptions options)
    {
      Assert.ArgumentNotNull(options, "options");

      // variables
      var uri = options.Uri;
      var filePath = options.FilePath;
      var networkReadBufferSize = options.NetworkReadBufferSize;
      var cookies = options.Cookies;
      var token = options.CancellationToken;
      var requestTimeout = options.RequestTimeout;
      var blockOptions = options.BlockOptions;

      // assert options for nulls
      Assert.IsNotNull(uri, "uri");
      Assert.IsNotNullOrEmpty(filePath, "filePath");
      Assert.IsNotNull(blockOptions, "blockOptions");

      // validate options
      Assert.IsTrue(networkReadBufferSize > 0, "networkReadBufferSize > 0");
      Assert.IsTrue(requestTimeout > TimeSpan.MinValue, "requestTimeout > TimeSpan.MinValue");

      // compute 
      var fileSize = Helper.GetFileSize(uri, cookies);
      var blocks = blockOptions.GetBlocks(fileSize).ToArray();
      Assert.IsTrue(blocks.Sum(x => x.Size) == fileSize);

      // create file
      using (File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
      {
      }

      // assign
      this.Uri = uri;
      this.FilePath = filePath;
      this.FileSize = fileSize;
      this.Blocks = blocks;
      this.Cookies = cookies;
      this.RequestTimeout = requestTimeout;
      this.BlocksCount = blocks.Length;
      this.NetworkReadBufferSize = networkReadBufferSize;
      this.cancellationToken = token;
      this.percentSize = Size.FromBytes(fileSize.Bytes / 100);
    }

    public void Download()
    {
      Assert.IsNotNull(this.Uri, "this.Uri");
      Assert.IsNotNull(this.FilePath, "this.FilePath");

      foreach (var block in this.Blocks)
      {
        var thread = new Thread(this.DownloadBlock);
        thread.Start(block);
      }
    }

    private void DownloadBlock(object param)
    {
      var block = (Block)param;
      Assert.IsNotNull(block, "block");

      try
      {
        if (this.cancellationToken.IsCancellationRequested || this.cancellationTokenSource.Token.IsCancellationRequested)
        {
          return;
        }

        var uri = this.Uri;
        var request = FormHelper.CreateRequest(uri);

        var requestTimeout = this.RequestTimeout;
        request.Timeout = (int)requestTimeout.TotalMilliseconds;

        var cookies = this.Cookies;
        if (cookies != null)
        {
          request.Headers[HttpRequestHeader.Cookie] = cookies;
        }

        var offset = block.Offset;
        var bytesLeft = block.Size.Bytes;
        var endPosition = offset + bytesLeft;
        request.AddRange(offset, endPosition);

        using (var response = (HttpWebResponse)request.GetResponse())
        {
          Assert.IsNotNull(response, "The requested URI doesn't respond. Uri: " + uri);

          using (var responseStream = response.GetResponseStream())
          {
            Assert.IsNotNull(responseStream, "The requested URI doesn't respond with a stream. Uri: " + uri);

            using (var fileStream = File.Open(this.FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
              fileStream.Position = offset;
              var buffer = new byte[this.NetworkReadBufferSize];
              while (true)
              {
                var bytesDownloaded = responseStream.Read(buffer, 0, Math.Min(buffer.Length, (int)bytesLeft));
                if (bytesDownloaded <= 0)
                {
                  break;
                }

                Assert.IsTrue(bytesLeft > 0);
                fileStream.Position = endPosition - bytesLeft;
                fileStream.Write(buffer, 0, bytesDownloaded);
                bytesLeft -= bytesDownloaded;

                // update progress if needed
                this.UpdateProgressCounter(block, bytesDownloaded);

                if (this.cancellationToken.IsCancellationRequested || this.cancellationTokenSource.Token.IsCancellationRequested)
                {
                  break;
                }
              }
            }
          }
        }

        Interlocked.Add(ref this.blocksCompleted, 1);
        lock (syncBlock)
        {
          if (this.blocksCompleted == this.BlocksCount && !this.downloadCompleted)
          {
            this.downloadCompleted = true;
            this.OnDownloadCompleted?.Invoke();
          }
        }
      }
      catch (Exception ex)
      {
        this.cancellationTokenSource.Cancel();

        this.OnErrorOccurred?.Invoke(ex);
      }
    }

    private void UpdateProgressCounter(Block block, int bytesDownloaded)
    {
      Interlocked.Add(ref this.totalBytesDownloaded, bytesDownloaded);
      var percent = this.totalBytesDownloaded / this.percentSize;
      if (percent > this.TotalPercentage)
      {
        this.TotalPercentage = percent;
        this.OnProgressChanged?.Invoke(block, percent);
      }

      if (this.totalBytesDownloaded == this.FileSize)
      {
        this.OnDownloadCompleted?.Invoke();
      }
    }

    public static class Helper
    {
      public static Size GetFileSize([NotNull] Uri uri, string cookies = null)
      {
        Assert.ArgumentNotNull(uri, "uri");

        var webRequest = FormHelper.CreateRequest(uri);
        webRequest.Headers[HttpRequestHeader.Cookie] = cookies;
        using (var response = webRequest.GetResponse())
        {
          return Size.FromBytes(response.ContentLength);
        }
      }
    }

    public class Block
    {
      public long Index
      { get; }

      public Size Size
      { get; }

      public long Offset
      { get; }

      public Block(long index, long offset, Size size)
      {
        this.Index = index;
        this.Offset = offset;
        this.Size = size;
      }
    }
  }
}
