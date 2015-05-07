namespace Alienlab.NetExtensions
{
  using System.Collections.Generic;

  public class MaxBlockSize : IBlockOptions
  {
    public Size Size { get; }

    public MaxBlockSize(Size size)
    {
      this.Size = size;
    }

    public IEnumerable<DownloadContext.Block> GetBlocks(Size fileSize)
    {
      var index = 0;
      var offset = 0L;
      var maxBlockSize = this.Size;
      while (fileSize >= maxBlockSize)
      {
        yield return new DownloadContext.Block(index++, offset, maxBlockSize);

        offset += maxBlockSize;
        fileSize -= maxBlockSize;
      }

      yield return new DownloadContext.Block(index, offset, fileSize);
    }
  }
}