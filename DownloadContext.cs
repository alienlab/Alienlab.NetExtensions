namespace Alienlab.NetExtensions
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Threading;
  using System.Threading.Tasks;

  public class DownloadContext
  {
    // used in Download
    const int pollingTimeout = 200;

    public readonly Uri Uri;

    [NotNull]
    public readonly string FilePath;

    public readonly Size FileSize;

    public readonly long NetworkReadBufferSize;

    public readonly string Cookies;

    public long BlocksCount { get; }

    protected readonly CancellationToken cancellationToken;

    [NotNull]
    protected readonly IEnumerable<Block> Blocks;

    protected readonly Size percentSize;

    protected readonly TimeSpan RequestTimeout;

    [NotNull]
    protected readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    [NotNull]
    protected readonly object syncBlock = new object();

    private long totalBytesDownloaded;

    private int blocksCompleted;

    private bool downloadCompleted;

    public event Action<Block, long, int> OnProgressChanged;

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

    public int BlocksCompleted => this.blocksCompleted;

    public virtual void DownloadAsync()
    {
      Assert.IsNotNull(this.Uri, "this.Uri");
      Assert.IsNotNull(this.FilePath, "this.FilePath");

      foreach (var block in this.Blocks)
      {
        this.StartDownloadBlock(block);
      }
    }

    public virtual void Download()
    {
      Assert.IsNotNull(this.Uri, "this.Uri");
      Assert.IsNotNull(this.FilePath, "this.FilePath");

      var threads = new List<Thread>();
      foreach (var block in this.Blocks)
      {
        Thread thread = this.StartDownloadBlock(block);
        threads.Add(thread);
      }

      while (threads.Any(x => x.IsAlive))
      {
        Thread.Sleep(pollingTimeout);
      }
    }

    protected virtual Thread StartDownloadBlock(Block block)
    {
      var thread = new Thread(this.DownloadBlock);
      thread.Start(block);

      return thread;
    }

    protected virtual void DownloadBlock(object param)
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
        
        var path = this.FilePath;
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

          HttpStatusCode statusCode = response.StatusCode;
          Assert.IsTrue(statusCode == HttpStatusCode.PartialContent, string.Format("The server response code is {0} instead of 206, which normally indicates that the server does not support range retrieval requests", (object) statusCode));
          
          using (var responseStream = response.GetResponseStream())
          {
            Assert.IsNotNull(responseStream, "The requested URI doesn't respond with a stream. Uri: " + uri);

            using (var fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
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

    protected virtual void UpdateProgressCounter(Block block, int bytesDownloaded)
    {
      Interlocked.Add(ref this.totalBytesDownloaded, bytesDownloaded);
      var percent = (int) (this.totalBytesDownloaded / this.percentSize);
      if (percent > this.TotalPercentage)
      {
        this.TotalPercentage = percent;
        this.OnProgressChanged?.Invoke(block, (long)bytesDownloaded, percent);
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
